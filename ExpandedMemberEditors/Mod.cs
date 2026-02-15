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
	public override string Version => "1.0.0";
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
			else if (member is ISyncDictionary dict)
			{
				ui.PushStyle();
				ui.Style.MinHeight = -1f;
				ui.VerticalLayout(4f);
				ui.Style.MinHeight = 24f;
				var text = ui.Text((LocaleString)(name + " (dictionary):"), bestFit: true, null, parseRTF: false);
				colorX color33 = dict.GetType().GetTypeColor().MulRGB(1.5f);
				InteractionElement.ColorDriver colorDriver = text.Slot.AttachComponent<Button>().ColorDrivers.Add();
				colorDriver.ColorDrive.Target = text.Color;
				colorDriver.NormalColor.Value = MathX.LerpUnclamped(RadiantUI_Constants.TEXT_COLOR, in color33, 0.1f);
				colorDriver.HighlightColor.Value = RadiantUI_Constants.LABEL_COLOR;
				colorDriver.PressColor.Value = RadiantUI_Constants.HEADING_COLOR;
				text.Slot.AttachComponent<ReferenceProxySource>().Reference.Target = dict;
				ui.Style.MinHeight = -1f;
				ui.VerticalLayout(4f);
				foreach (var kvp in dict.BoxedEntries)
				{
					SyncMemberEditorBuilder.Build(kvp.Value, kvp.Key.ToString()!, null!, ui, labelSize);
				}
				ui.NestOut();
				ui.NestOut();
				ui.PopStyle();
			}
		}
	}
}