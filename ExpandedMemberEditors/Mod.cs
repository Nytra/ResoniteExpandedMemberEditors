using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using ResoniteModLoader;
using System.Reflection;
using FrooxEngine.ProtoFlux;

#if DEBUG
using ResoniteHotReloadLib;
#endif

namespace ExpandedMemberEditors;

public class ExpandedMemberEditors : ResoniteMod
{
	public override string Name => "Expanded Member Editors";
	public override string Author => "Nytra";
	public override string Version => "1.1.0";
	public override string Link => "https://github.com/Nytra/ResoniteExpandedMemberEditors";

	private static Harmony harmony = new Harmony("owo.Nytra.ExpandedMemberEditors");

	[AutoRegisterConfigKey] static ModConfigurationKey<bool> Key_Enabled = new("enabled", "should the mod be enabled", () => true);
	[AutoRegisterConfigKey] static ModConfigurationKey<bool> Key_VarProxySources = new("varProxySources", "should the mod generate reference proxy sources for syncvars", () => false);

	static ModConfiguration? config;

	public override void OnEngineInit()
	{
		config = GetConfiguration();
#if DEBUG
		HotReloader.RegisterForHotReload(this);
#endif
		Engine.Current.RunPostInit(InitializeMod);
	}

	static void InitializeMod()
	{
		harmony.PatchAll();
	}

#if DEBUG
	static void BeforeHotReload()
	{
		harmony.UnpatchAll(harmony.Id);
	}

	static void OnHotReload(ResoniteMod modInstance)
	{
		config = modInstance.GetConfiguration();
		InitializeMod();
	}
#endif

	[HarmonyPatch(typeof(SyncMemberEditorBuilder), "BuildDictionary")]
	class ExpandedMemberEditorsPatch2
	{
		static void BuildDictionaryElements(ISyncDictionary dict, UIBuilder ui, float labelSize)
		{
			foreach (var kvp in dict.BoxedEntries)
			{
				ui.PushStyle();
				ui.Style.MinHeight = 24f;
				ui.Style.FlexibleWidth = 1f;
				ui.HorizontalLayout(4f);
				SyncMemberEditorBuilder.Build(kvp.Value, kvp.Key.ToString()!, null!, ui, labelSize);
				ui.PushStyle();
				ui.Style.FlexibleWidth = -1f;
				ui.Button("Remove").LocalPressed += (btn, data) =>
				{
					dict.RemoveByKey(kvp.Key);
				};
				ui.PopStyle();
				ui.NestOut();
				ui.PopStyle();
			}
		}
		static bool Prefix(ISyncDictionary dictionary, string name, FieldInfo fieldInfo, UIBuilder ui, float labelSize)
		{
			if (!config!.GetValue(Key_Enabled)) return true;

			ui.PushStyle();
			ui.Style.MinHeight = -1f;

			ui.VerticalLayout(4f);
			ui.Style.MinHeight = 24f;
			var text = ui.Text((LocaleString)(name + " (dictionary):"), bestFit: true, null, parseRTF: false);
			colorX color33 = dictionary.GetType().GetTypeColor().MulRGB(1.5f);
			InteractionElement.ColorDriver colorDriver = text.Slot.AttachComponent<Button>().ColorDrivers.Add();
			colorDriver.ColorDrive.Target = text.Color;
			colorDriver.NormalColor.Value = MathX.LerpUnclamped(RadiantUI_Constants.TEXT_COLOR, in color33, 0.1f);
			colorDriver.HighlightColor.Value = RadiantUI_Constants.LABEL_COLOR;
			colorDriver.PressColor.Value = RadiantUI_Constants.HEADING_COLOR;
			text.Slot.AttachComponent<ReferenceProxySource>().Reference.Target = dictionary;
			ui.Style.MinHeight = -1f;

			ui.VerticalLayout(4f);
			var listRoot = ui.Root;
			BuildDictionaryElements(dictionary, ui, labelSize);
			dictionary.Changed += (changeable) =>
			{
				if (listRoot.FilterWorldElement() is null) return;
				listRoot.DestroyChildren();
				ui.NestInto(listRoot);
				BuildDictionaryElements(dictionary, ui, labelSize);
			};
			ui.NestOut();

			ui.PushStyle();
			ui.Style.MinHeight = 24f;
			ui.Style.FlexibleWidth = 1f;

			ui.HorizontalLayout(4f);

			ui.PushStyle();
			ui.Style.FlexibleWidth = -1f;
			ui.Text("Key:");
			ui.PopStyle();

			ui.PushStyle();
			ui.Style.MinWidth = 120f;
			var keyComp = ui.CurrentRect.Slot.AttachComponent(typeof(ValueField<>).MakeGenericType(dictionary.KeyType));
			ui.PrimitiveMemberEditor((IField)keyComp.GetSyncMember("Value"));
			ui.PopStyle();

			var valueType = dictionary.GetType().GetGenericArguments()[1];
			if (valueType.IsEnginePrimitive())
			{
				ui.PushStyle();
				ui.Style.FlexibleWidth = -1f;
				ui.Text("Value:");
				ui.PopStyle();

				ui.PushStyle();
				ui.Style.MinWidth = 240f;
				var valueComp = ui.CurrentRect.Slot.AttachComponent(typeof(ValueField<>).MakeGenericType(valueType));
				ui.PrimitiveMemberEditor((IField)valueComp.GetSyncMember("Value"));
				ui.PopStyle();

				ui.Button("Add").LocalPressed += (btn, data) =>
				{
					var member = dictionary.Add(((IField)keyComp.GetSyncMember("Value")).BoxedValue);
					var field = (IField)member;
					field.BoxedValue = ((IField)valueComp.GetSyncMember("Value")).BoxedValue;
				};
			}
			else
			{
				ui.PushStyle();
				ui.Style.FlexibleWidth = -1f;
				ui.Text("Reference:");
				ui.PopStyle();

				ui.PushStyle();
				ui.Style.MinWidth = 240f;
				var refComp = ui.CurrentRect.Slot.AttachComponent(typeof(ReferenceField<>).MakeGenericType(valueType));
				ui.RefMemberEditor((ISyncRef)refComp.GetSyncMember("Reference"));
				ui.PopStyle();

				ui.Button("Add").LocalPressed += (btn, data) =>
				{
					var member = dictionary.Add(((IField)keyComp.GetSyncMember("Value")).BoxedValue);
					var field = (IField)member;
					field.BoxedValue = ((IField)refComp.GetSyncMember("Reference")).BoxedValue;
				};
			}
			ui.PopStyle();
			ui.NestOut();
			ui.NestOut();
			ui.PopStyle();
			return false;
		}
	}

	[HarmonyPatch(typeof(SyncMemberEditorBuilder), "Build")]
	class ExpandedMemberEditorsPatch
	{
		static void Postfix(ISyncMember member, string name, FieldInfo fieldInfo, UIBuilder ui, float labelSize = 0.3f)
		{
			if (!config!.GetValue(Key_Enabled)) return;

			if (member is SyncVar syncVar)
			{
				ui.PushStyle();
				ui.Style.MinHeight = -1f;

				ui.VerticalLayout(4f);

				var genProxySource = config.GetValue(Key_VarProxySources);

				if (genProxySource)
				{
					ui.Style.MinHeight = 24f;
					var text = ui.Text((LocaleString)(name + " (var):"), bestFit: true, null, parseRTF: false);
					colorX color33 = syncVar.GetType().GetTypeColor().MulRGB(1.5f);
					InteractionElement.ColorDriver colorDriver = text.Slot.AttachComponent<Button>().ColorDrivers.Add();
					colorDriver.ColorDrive.Target = text.Color;
					colorDriver.NormalColor.Value = MathX.LerpUnclamped(RadiantUI_Constants.TEXT_COLOR, in color33, 0.1f);
					colorDriver.HighlightColor.Value = RadiantUI_Constants.LABEL_COLOR;
					colorDriver.PressColor.Value = RadiantUI_Constants.HEADING_COLOR;
					text.Slot.AttachComponent<ReferenceProxySource>().Reference.Target = syncVar;
				}

				if (syncVar.Element is not null)
				{
					SyncMemberEditorBuilder.Build(syncVar.Element, name, null!, ui, labelSize);
				}
				else
				{
					ui.Style.MinHeight = 24f;
					ui.Text($"{(genProxySource ? "" : $"{name}: ")}<null>");
				}

				ui.NestOut();
				ui.PopStyle();
			}
		}
	}
}