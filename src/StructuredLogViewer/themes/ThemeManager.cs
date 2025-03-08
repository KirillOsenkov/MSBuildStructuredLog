using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
//using AdonisUI;
using ResourceLocator = AdonisUI.ResourceLocator;

namespace StructuredLogViewer
{
    public class ThemeManager
    {
        static ThemeManager()
        {
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        }

        public static bool UseDarkTheme { get; set; }

        private static void SystemParameters_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast))
            {
                UpdateTheme();
            }
        }

        private static bool useAdonisDarkTheme;
        public static bool UseAdonisDarkTheme
        {
            get => useAdonisDarkTheme;

            set
            {
                if (useAdonisDarkTheme == value)
                {
                    return;
                }

                useAdonisDarkTheme = value;
                var appResources = Application.Current.Resources;
                if (value)
                {
                    ResourceLocator.SetColorScheme(appResources, ResourceLocator.DarkColorScheme);

                    appResources.MergedDictionaries.Add(
                        new ResourceDictionary()
                        {
                            Source = new Uri("pack://application:,,,/AdonisUI;component/ColorSchemes/dark.xaml")
                        });
                    appResources.MergedDictionaries.Add(
                        new ResourceDictionary()
                        {
                            Source = new Uri("pack://application:,,,/AdonisUI.ClassicTheme;component/Resources.xaml")
                        });

                    appResources[AdonisUI.Colors.Layer0BackgroundColor] = GetColor("#2A2B2F");
                    appResources[AdonisUI.Colors.Layer0BorderColor] = GetColor("#2A2B2F");
                    appResources[AdonisUI.Colors.Layer1BackgroundColor] = GetColor(BackgroundColor);
                    appResources[AdonisUI.Colors.Layer2HighlightColor] = GetColor(BackgroundColor);
                }
                else
                {
                    ResourceLocator.RemoveAdonisResources(appResources);
                }
            }
        }

        public static readonly string BackgroundColor = "#303030";
        public static readonly Color Background = GetColor(BackgroundColor);
        public static readonly Brush BackgroundBrush = new SolidColorBrush(Background);
        public static readonly Color LighterBackground = GetColor("#454545");
        public static readonly Brush LighterBackgroundBrush = new SolidColorBrush(LighterBackground);
        public static readonly Color ControlText = Color.FromRgb(200, 200, 200);
        public static readonly Brush ControlTextBrush = new SolidColorBrush(ControlText);

        private static readonly BrushConverter brushConverter = new BrushConverter();
        public static Brush GetBrush(string hex) => (Brush)brushConverter.ConvertFromString(hex);
        public static Color GetColor(string hex) => (Color)ColorConverter.ConvertFromString(hex);

        public static void UpdateTheme()
        {
            var buttonStaticBackground = "#FFF0F0F0";
            var buttonStaticBorder = "#FF707070";
            var buttonMouseOverBackground = "#FFBEE6FD";
            var buttonMouseOverBorder = "#FF3C7FB1";
            var buttonPressedBackground = "#FFC4E5F6";
            var buttonPressedBorder = "#FF2C628B";
            var buttonCheckedBackground = "#FFBCDDEE";
            var buttonCheckedBorder = "#FF245A83";
            var buttonDisabledBackground = "#FFF4F4F4";
            var buttonDisabledBorder = "#FFADB2B5";
            var buttonDisabledForeground = "#FF838383";

            var comboBoxStaticBackground = Gradient("#FFF0F0F0", "#FFE5E5E5");
            var comboBoxStaticBorder = "#FFACACAC";
            var comboBoxStaticGlyph = "#FF606060";
            var comboBoxStaticEditableBackground = "White";
            var comboBoxStaticEditableBorder = "#FFABADB3";
            var comboBoxStaticEditableButtonBackground = "Transparent";
            var comboBoxStaticEditableButtonBorder = "Transparent";

            var comboBoxMouseOverBackground = Gradient("#FFECF4FC", "#FFDCECFC");
            var comboBoxMouseOverBorder = "#FF7EB4EA";
            var comboBoxMouseOverGlyph = "Black";
            var comboBoxMouseOverEditableBackground = "White";
            var comboBoxMouseOverEditableBorder = "#FF7EB4EA";
            var comboBoxMouseOverEditableButtonBackground = Gradient("#FFEBF4FC", "#FFDCECFC");
            var comboBoxMouseOverEditableButtonBorder = "#FF7EB4EA";

            var comboBoxPressedBackground = Gradient("#FFDAECFC", "#FFC4E0FC");
            var comboBoxPressedBorder = "#FF569DE5";
            var comboBoxPressedGlyph = "Black";
            var comboBoxPressedEditableBackground = "White";
            var comboBoxPressedEditableBorder = "#FF569DE5";
            var comboBoxPressedEditableButtonBackground = Gradient("#FFDAEBFC", "#FFC4E0FC");
            var comboBoxPressedEditableButtonBorder = "#FF569DE5";

            var comboBoxDisabledBackground = "#FFF0F0F0";
            var comboBoxDisabledBorder = "#FFD9D9D9";
            var comboBoxDisabledGlyph = "#FFBFBFBF";
            var comboBoxDisabledEditableBackground = "#FFFFFFFF";
            var comboBoxDisabledEditableBorder = "#FFBFBFBF";
            var comboBoxDisabledEditableButtonBackground = "Transparent";
            var comboBoxDisabledEditableButtonBorder = "Transparent";

            var comboBoxItemItemsviewHoverBackground = "#1F26A0DA";
            var comboBoxItemItemsviewHoverBorder = "#A826A0DA";
            var comboBoxItemItemsviewSelectedBackground = "#3D26A0DA";
            var comboBoxItemItemsviewSelectedBorder = "#FF26A0DA";
            var comboBoxItemItemsviewSelectedHoverBackground = "#2E0080FF";
            var comboBoxItemItemsviewSelectedHoverBorder = "#99006CD9";
            var comboBoxItemItemsviewSelectedNoFocusBackground = "#3DDADADA";
            var comboBoxItemItemsviewSelectedNoFocusBorder = "#FFDADADA";
            var comboBoxItemItemsviewFocusBorder = "#FF26A0DA";
            var comboBoxItemItemsviewHoverFocusBackground = "#5426A0DA";
            var comboBoxItemItemsviewHoverFocusBorder = "#FF26A0DA";

            var contextMenuStaticBackground = "#F5F5F5";
            var contextMenuStaticBorderBrush = "#FF959595";
            var contextMenuHeaderBackground = "#F1F1F1";
            var contextMenuHeaderLeftBorderBrush = "#E2E3E3";
            var contextMenuHeaderRightBorderBrush = "White";

            var optionMarkStaticBackground = "#FFFFFFFF";
            var optionMarkStaticBorder = "#FF707070";
            var optionMarkStaticGlyph = "#FF212121";
            var optionMarkMouseOverBackground = "#FFF3F9FF";
            var optionMarkMouseOverBorder = "#FF5593FF";
            var optionMarkMouseOverGlyph = "#FF212121";
            var optionMarkPressedBackground = "#FFD9ECFF";
            var optionMarkPressedBorder = "#FF3C77DD";
            var optionMarkPressedGlyph = "#FF212121";
            var optionMarkDisabledBackground = "#FFE6E6E6";
            var optionMarkDisabledBorder = "#FFBCBCBC";
            var optionMarkDisabledGlyph = "#FF707070";

            var groupBoxOuterBorder = "White";
            var groupBoxMiddleBorder = "#D5DFD5";
            var groupBoxInnerBorder = "White";

            var menuStaticBackground = "#FFF0F0F0";
            var menuStaticBorder = "#FF999999";
            var menuStaticForeground = "#FF212121";
            var menuStaticSeparator = "#FFD7D7D7";
            var menuDisabledBackground = "#3DDADADA";
            var menuDisabledBorder = "#FFDADADA";
            var menuDisabledForeground = "#FF707070";
            var menuItemSelectedBackground = "#3D26A0DA";
            var menuItemSelectedBorder = "#FF26A0DA";
            var menuItemHighlightBackground = "#3D26A0DA";
            var menuItemHighlightBorder = "#FF26A0DA";
            var menuItemHighlightDisabledBackground = "#0A000000";
            var menuItemHighlightDisabledBorder = "#21000000";

            var scrollBarStaticBackground = "#F0F0F0";
            var scrollBarStaticBorder = "#F0F0F0";
            var scrollBarStaticGlyph = "#606060";
            var scrollBarStaticThumb = "#CDCDCD";
            var scrollBarMouseOverBackground = "#DADADA";
            var scrollBarMouseOverBorder = "#DADADA";
            var scrollBarMouseOverGlyph = "#000000";
            var scrollBarMouseOverThumb = "#A6A6A6";
            var scrollBarPressedBackground = "#606060";
            var scrollBarPressedBorder = "#606060";
            var scrollBarPressedThumb = "#606060";
            var scrollBarPressedGlyph = "#FFFFFF";
            var scrollBarDisabledBackground = "#F0F0F0";
            var scrollBarDisabledBorder = "#F0F0F0";
            var scrollBarDisabledGlyph = "#BFBFBF";
            var scrollBarDisabledThumb = "#F0F0F0";

            var tabItemStaticBackground = "Transparent";
            var tabItemStaticBorder = "#ACACAC";
            var tabItemSelectedBackground = "#FFFFFF";
            var tabItemSelectedBorder = "#ACACAC";
            var tabItemMouseOverBackground = "#ECF4FC";
            var tabItemMouseOverBorder = "#7EB4EA";
            var tabItemDisabledBackground = "#F0F0F0";
            var tabItemDisabledBorder = "#D9D9D9";

            if (SystemParameters.HighContrast)
            {
                UseAdonisDarkTheme = false;
                SetResource("Theme_Background", SystemColors.AppWorkspaceBrush);
                SetResource("Theme_WhiteBackground", SystemColors.ControlBrush);
                SetResource("Theme_ToolWindowBackground", SystemColors.ControlBrush);
                SetResource("ImportStroke", Brushes.Sienna);
                SetResource("NoImportStroke", GetBrush("#FF0000"));
                SetResource("NoImportFill", Brushes.BlanchedAlmond);
                SetResource("NuGet", Brushes.DeepSkyBlue);
                SetResource(SystemColors.MenuBarBrushKey, SystemColors.MenuBarBrush);

                SetDefaultSystemColors();
            }
            else if (UseDarkTheme)
            {
                var color500 = "#9E9E9E";
                var color600 = "#757575";
                var color700 = "#616161";
                var color800 = "#424242";
                var color850 = "#303030";
                var color900 = "#212121";
                var foregroundColor = "#e5ffffff";
                var foregroundDisabledColor = "#50ffffff";
                var foregroundSecondaryColor = "#b3ffffff";
                var selectionColor1 = "#3b5464";
                var selectionColor1Opacity05 = "#803b5464";
                var selectionColor2 = "#36A2DB";

                SetResource("Theme_Background", LighterBackgroundBrush);
                SetResource("Theme_WhiteBackground", BackgroundBrush);
                SetResource("Theme_ToolWindowBackground", BackgroundBrush);
                SetResource("Theme_InfoBarBackground", GetBrush("#202040"));

                UseAdonisDarkTheme = false;

                SetResource(SystemColors.ActiveBorderBrushKey, GetBrush(color700));
                SetResource(SystemColors.ControlBrushKey, LighterBackgroundBrush);
                SetResource(SystemColors.ControlTextBrushKey, ControlTextBrush);
                SetResource(SystemColors.HighlightBrushKey, Brushes.SlateBlue);
                SetResource(SystemColors.InactiveSelectionHighlightBrushKey, Brushes.DimGray);
                SetResource(SystemColors.WindowBrushKey, BackgroundBrush);

                SetResource(SystemColors.MenuBarBrushKey, LighterBackgroundBrush);
                SetResource(SystemColors.MenuHighlightBrushKey, LighterBackgroundBrush);
                SetResource(SystemColors.MenuTextBrushKey, ControlTextBrush);
                SetResource(SystemColors.MenuBrushKey, BackgroundBrush);
                SetResource(SystemColors.MenuBarColorKey, Background);
                SetResource(SystemColors.MenuHighlightColorKey, LighterBackground);
                SetResource(SystemColors.MenuTextColorKey, ControlText);
                SetResource(SystemColors.MenuColorKey, Background);

                SetResource("ImportStroke", GetBrush("#F08244"));
                SetResource("NoImportStroke", GetBrush("#FFCCCC"));
                SetResource("NoImportFill", GetBrush("#474138"));
                SetResource("TargetStroke", GetBrush("#C0A0F0"));
                SetResource("AddItemStroke", GetBrush("#40B0B0"));
                SetResource("NuGet", Brushes.DeepSkyBlue);
                SetResource("\u01D6", GetBrush("#C0C0C0"));

                buttonStaticBackground = color800;
                buttonStaticBorder = color700;
                buttonMouseOverBackground = color700;
                buttonMouseOverBorder = color600;
                buttonPressedBackground = color600;
                buttonPressedBorder = color500;
                buttonCheckedBackground = selectionColor1;
                buttonCheckedBorder = selectionColor2;
                buttonDisabledBackground = color850;
                buttonDisabledBorder = color800;
                buttonDisabledForeground = foregroundDisabledColor;

                comboBoxStaticBackground = GetBrush(color800);
                comboBoxStaticBorder = color700;
                comboBoxStaticGlyph = foregroundColor;
                comboBoxStaticEditableBackground = color800;
                comboBoxStaticEditableBorder = color700;
                comboBoxStaticEditableButtonBackground = "Transparent";
                comboBoxStaticEditableButtonBorder = "Transparent";

                comboBoxMouseOverBackground = GetBrush(color700);
                comboBoxMouseOverBorder = color600;
                comboBoxMouseOverGlyph = foregroundColor;
                comboBoxMouseOverEditableBackground = color700;
                comboBoxMouseOverEditableBorder = color600;
                comboBoxMouseOverEditableButtonBackground = GetBrush(color700);
                comboBoxMouseOverEditableButtonBorder = color600;

                comboBoxPressedBackground = GetBrush(color600);
                comboBoxPressedBorder = color500;
                comboBoxPressedGlyph = foregroundColor;
                comboBoxPressedEditableBackground = color600;
                comboBoxPressedEditableBorder = color500;
                comboBoxPressedEditableButtonBackground = GetBrush(color600);
                comboBoxPressedEditableButtonBorder = color500;

                comboBoxDisabledBackground = color850;
                comboBoxDisabledBorder = color800;
                comboBoxDisabledGlyph = foregroundDisabledColor;
                comboBoxDisabledEditableBackground = "Transparent";
                comboBoxDisabledEditableBorder = color800;
                comboBoxDisabledEditableButtonBackground = color850;
                comboBoxDisabledEditableButtonBorder = color850;

                comboBoxItemItemsviewHoverBackground = selectionColor1;
                comboBoxItemItemsviewHoverBorder = selectionColor2;
                comboBoxItemItemsviewSelectedBackground = selectionColor1;
                comboBoxItemItemsviewSelectedBorder = selectionColor2;
                comboBoxItemItemsviewSelectedHoverBackground = selectionColor1;
                comboBoxItemItemsviewSelectedHoverBorder = selectionColor2;
                comboBoxItemItemsviewSelectedNoFocusBackground = "#3DDADADA";
                comboBoxItemItemsviewSelectedNoFocusBorder = "#FFDADADA";
                comboBoxItemItemsviewFocusBorder = selectionColor2;
                comboBoxItemItemsviewHoverFocusBackground = selectionColor1;
                comboBoxItemItemsviewHoverFocusBorder = selectionColor2;

                contextMenuStaticBackground = color800;
                contextMenuStaticBorderBrush = color700;
                contextMenuHeaderBackground = color800;
                contextMenuHeaderLeftBorderBrush = foregroundSecondaryColor;
                contextMenuHeaderRightBorderBrush = "Transparent";

                optionMarkStaticBackground = color800;
                optionMarkStaticBorder = color700;
                optionMarkStaticGlyph = foregroundColor;
                optionMarkMouseOverBackground = color700;
                optionMarkMouseOverBorder = color600;
                optionMarkMouseOverGlyph = foregroundColor;
                optionMarkPressedBackground = color600;
                optionMarkPressedBorder = color500;
                optionMarkPressedGlyph = foregroundColor;
                optionMarkDisabledBackground = color850;
                optionMarkDisabledBorder = color800;
                optionMarkDisabledGlyph = foregroundDisabledColor;

                groupBoxOuterBorder = color850;
                groupBoxMiddleBorder = color700;
                groupBoxInnerBorder = color850;

                menuStaticBackground = color800;
                menuStaticBorder = color700;
                menuStaticForeground = foregroundColor;
                menuStaticSeparator = color500;
                menuDisabledBackground = color850;
                menuDisabledBorder = color800;
                menuDisabledForeground = foregroundDisabledColor;
                menuItemSelectedBackground = selectionColor1;
                menuItemSelectedBorder = selectionColor2;
                menuItemHighlightBackground = selectionColor1Opacity05;
                menuItemHighlightBorder = selectionColor2;
                menuItemHighlightDisabledBackground = color850;
                menuItemHighlightDisabledBorder = color800;

                scrollBarStaticBackground = color800;
                scrollBarStaticBorder = color800;
                scrollBarStaticGlyph = foregroundColor;
                scrollBarStaticThumb = color700;
                scrollBarMouseOverBackground = color700;
                scrollBarMouseOverBorder = color700;
                scrollBarMouseOverGlyph = foregroundColor;
                scrollBarMouseOverThumb = color600;
                scrollBarPressedBackground = color600;
                scrollBarPressedBorder = color600;
                scrollBarPressedThumb = color500;
                scrollBarPressedGlyph = foregroundColor;
                scrollBarDisabledBackground = color800;
                scrollBarDisabledBorder = color800;
                scrollBarDisabledGlyph = foregroundDisabledColor;
                scrollBarDisabledThumb = color700;

                tabItemStaticBackground = color800;
                tabItemStaticBorder = color700;
                tabItemSelectedBackground = color850;
                tabItemSelectedBorder = color800;
                tabItemMouseOverBackground = color700;
                tabItemMouseOverBorder = color600;
                tabItemDisabledBackground = color850;
                tabItemDisabledBorder = color800;
            }
            else
            {
                UseAdonisDarkTheme = false;
                SetResource("Theme_Background", new SolidColorBrush(Color.FromRgb(238, 238, 242)));
                SetResource("Theme_WhiteBackground", Brushes.White);
                SetResource("Theme_ToolWindowBackground", Brushes.WhiteSmoke);
                SetResource("ImportStroke", Brushes.Sienna);
                SetResource("NoImportStroke", GetBrush("#FF0000"));
                SetResource("NoImportFill", Brushes.BlanchedAlmond);
                SetResource("TargetStroke", Brushes.MediumPurple);
                SetResource("AddItemStroke", Brushes.Teal);
                SetResource("NuGet", GetBrush("#004880"));
                SetResource("\u01D6", GetBrush("#595959"));
                SetResource(SystemColors.MenuBarBrushKey, "#F5F5F5");

                SetDefaultSystemColors();
            }

            SetResource("Button.Static.Background", GetBrush(buttonStaticBackground));
            SetResource("Button.Static.Border", GetBrush(buttonStaticBorder));
            SetResource("Button.MouseOver.Background", GetBrush(buttonMouseOverBackground));
            SetResource("Button.MouseOver.Border", GetBrush(buttonMouseOverBorder));
            SetResource("Button.Pressed.Background", GetBrush(buttonPressedBackground));
            SetResource("Button.Pressed.Border", GetBrush(buttonPressedBorder));
            SetResource("Button.Checked.Background", GetBrush(buttonCheckedBackground));
            SetResource("Button.Checked.Border", GetBrush(buttonCheckedBorder));
            SetResource("Button.Disabled.Background", GetBrush(buttonDisabledBackground));
            SetResource("Button.Disabled.Border", GetBrush(buttonDisabledBorder));
            SetResource("Button.Disabled.Foreground", GetBrush(buttonDisabledForeground));

            SetResource("ComboBox.Static.Background", comboBoxStaticBackground);
            SetResource("ComboBox.Static.Border", GetBrush(comboBoxStaticBorder));
            SetResource("ComboBox.Static.Glyph", GetBrush(comboBoxStaticGlyph));
            SetResource("ComboBox.Static.Editable.Background", GetBrush(comboBoxStaticEditableBackground));
            SetResource("ComboBox.Static.Editable.Border", GetBrush(comboBoxStaticEditableBorder));
            SetResource("ComboBox.Static.Editable.Button.Background", GetBrush(comboBoxStaticEditableButtonBackground));
            SetResource("ComboBox.Static.Editable.Button.Border", GetBrush(comboBoxStaticEditableButtonBorder));
            SetResource("ComboBox.MouseOver.Background", comboBoxMouseOverBackground);
            SetResource("ComboBox.MouseOver.Border", GetBrush(comboBoxMouseOverBorder));
            SetResource("ComboBox.MouseOver.Glyph", GetBrush(comboBoxMouseOverGlyph));
            SetResource("ComboBox.MouseOver.Editable.Background", GetBrush(comboBoxMouseOverEditableBackground));
            SetResource("ComboBox.MouseOver.Editable.Border", GetBrush(comboBoxMouseOverEditableBorder));
            SetResource("ComboBox.MouseOver.Editable.Button.Background", comboBoxMouseOverEditableButtonBackground);
            SetResource("ComboBox.MouseOver.Editable.Button.Border", GetBrush(comboBoxMouseOverEditableButtonBorder));
            SetResource("ComboBox.Pressed.Background", comboBoxPressedBackground);
            SetResource("ComboBox.Pressed.Border", GetBrush(comboBoxPressedBorder));
            SetResource("ComboBox.Pressed.Glyph", GetBrush(comboBoxPressedGlyph));
            SetResource("ComboBox.Pressed.Editable.Background", GetBrush(comboBoxPressedEditableBackground));
            SetResource("ComboBox.Pressed.Editable.Border", GetBrush(comboBoxPressedEditableBorder));
            SetResource("ComboBox.Pressed.Editable.Button.Background", comboBoxPressedEditableButtonBackground);
            SetResource("ComboBox.Pressed.Editable.Button.Border", GetBrush(comboBoxPressedEditableButtonBorder));
            SetResource("ComboBox.Disabled.Background", GetBrush(comboBoxDisabledBackground));
            SetResource("ComboBox.Disabled.Border", GetBrush(comboBoxDisabledBorder));
            SetResource("ComboBox.Disabled.Glyph", GetBrush(comboBoxDisabledGlyph));
            SetResource("ComboBox.Disabled.Editable.Background", GetBrush(comboBoxDisabledEditableBackground));
            SetResource("ComboBox.Disabled.Editable.Border", GetBrush(comboBoxDisabledEditableBorder));
            SetResource("ComboBox.Disabled.Editable.Button.Background", GetBrush(comboBoxDisabledEditableButtonBackground));
            SetResource("ComboBox.Disabled.Editable.Button.Border", GetBrush(comboBoxDisabledEditableButtonBorder));
            SetResource("ComboBoxItem.ItemsviewHover.Background", GetBrush(comboBoxItemItemsviewHoverBackground));
            SetResource("ComboBoxItem.ItemsviewHover.Border", GetBrush(comboBoxItemItemsviewHoverBorder));
            SetResource("ComboBoxItem.ItemsviewSelected.Background", GetBrush(comboBoxItemItemsviewSelectedBackground));
            SetResource("ComboBoxItem.ItemsviewSelected.Border", GetBrush(comboBoxItemItemsviewSelectedBorder));
            SetResource("ComboBoxItem.ItemsviewSelectedHover.Background", GetBrush(comboBoxItemItemsviewSelectedHoverBackground));
            SetResource("ComboBoxItem.ItemsviewSelectedHover.Border", GetBrush(comboBoxItemItemsviewSelectedHoverBorder));
            SetResource("ComboBoxItem.ItemsviewSelectedNoFocus.Background", GetBrush(comboBoxItemItemsviewSelectedNoFocusBackground));
            SetResource("ComboBoxItem.ItemsviewSelectedNoFocus.Border", GetBrush(comboBoxItemItemsviewSelectedNoFocusBorder));
            SetResource("ComboBoxItem.ItemsviewFocus.Border", GetBrush(comboBoxItemItemsviewFocusBorder));
            SetResource("ComboBoxItem.ItemsviewHoverFocus.Background", GetBrush(comboBoxItemItemsviewHoverFocusBackground));
            SetResource("ComboBoxItem.ItemsviewHoverFocus.Border", GetBrush(comboBoxItemItemsviewHoverFocusBorder));

            SetResource("ContextMenu.Static.Background", GetBrush(contextMenuStaticBackground));
            SetResource("ContextMenu.Static.BorderBrush", GetBrush(contextMenuStaticBorderBrush));
            SetResource("ContextMenu.Header.Background", GetBrush(contextMenuHeaderBackground));
            SetResource("ContextMenu.Header.LeftBorderBrush", GetBrush(contextMenuHeaderLeftBorderBrush));
            SetResource("ContextMenu.Header.RightBorderBrush", GetBrush(contextMenuHeaderRightBorderBrush));

            SetResource("OptionMark.Static.Background", GetBrush(optionMarkStaticBackground));
            SetResource("OptionMark.Static.Border", GetBrush(optionMarkStaticBorder));
            SetResource("OptionMark.Static.Glyph", GetBrush(optionMarkStaticGlyph));
            SetResource("OptionMark.MouseOver.Background", GetBrush(optionMarkMouseOverBackground));
            SetResource("OptionMark.MouseOver.Border", GetBrush(optionMarkMouseOverBorder));
            SetResource("OptionMark.MouseOver.Glyph", GetBrush(optionMarkMouseOverGlyph));
            SetResource("OptionMark.Pressed.Background", GetBrush(optionMarkPressedBackground));
            SetResource("OptionMark.Pressed.Border", GetBrush(optionMarkPressedBorder));
            SetResource("OptionMark.Pressed.Glyph", GetBrush(optionMarkPressedGlyph));
            SetResource("OptionMark.Disabled.Background", GetBrush(optionMarkDisabledBackground));
            SetResource("OptionMark.Disabled.Border", GetBrush(optionMarkDisabledBorder));
            SetResource("OptionMark.Disabled.Glyph", GetBrush(optionMarkDisabledGlyph));

            SetResource("GroupBox.Static.OuterBorder", GetBrush(groupBoxOuterBorder));
            SetResource("GroupBox.Static.MiddleBorder", GetBrush(groupBoxMiddleBorder));
            SetResource("GroupBox.Static.InnerBorder", GetBrush(groupBoxInnerBorder));

            SetResource("Menu.Static.Background", GetBrush(menuStaticBackground));
            SetResource("Menu.Static.Border", GetBrush(menuStaticBorder));
            SetResource("Menu.Static.Foreground", GetBrush(menuStaticForeground));
            SetResource("Menu.Static.Separator", GetBrush(menuStaticSeparator));
            SetResource("Menu.Disabled.Background", GetBrush(menuDisabledBackground));
            SetResource("Menu.Disabled.Border", GetBrush(menuDisabledBorder));
            SetResource("Menu.Disabled.Foreground", GetBrush(menuDisabledForeground));
            SetResource("MenuItem.Selected.Background", GetBrush(menuItemSelectedBackground));
            SetResource("MenuItem.Selected.Border", GetBrush(menuItemSelectedBorder));
            SetResource("MenuItem.Highlight.Background", GetBrush(menuItemHighlightBackground));
            SetResource("MenuItem.Highlight.Border", GetBrush(menuItemHighlightBorder));
            SetResource("MenuItem.Highlight.Disabled.Background", GetBrush(menuItemHighlightDisabledBackground));
            SetResource("MenuItem.Highlight.Disabled.Border", GetBrush(menuItemHighlightDisabledBorder));

            SetResource("ScrollBar.Static.Background", GetBrush(scrollBarStaticBackground));
            SetResource("ScrollBar.Static.Border", GetBrush(scrollBarStaticBorder));
            SetResource("ScrollBar.Static.Glyph", GetBrush(scrollBarStaticGlyph));
            SetResource("ScrollBar.Static.Thumb", GetBrush(scrollBarStaticThumb));
            SetResource("ScrollBar.MouseOver.Background", GetBrush(scrollBarMouseOverBackground));
            SetResource("ScrollBar.MouseOver.Border", GetBrush(scrollBarMouseOverBorder));
            SetResource("ScrollBar.MouseOver.Glyph", GetBrush(scrollBarMouseOverGlyph));
            SetResource("ScrollBar.MouseOver.Thumb", GetBrush(scrollBarMouseOverThumb));
            SetResource("ScrollBar.Pressed.Background", GetBrush(scrollBarPressedBackground));
            SetResource("ScrollBar.Pressed.Border", GetBrush(scrollBarPressedBorder));
            SetResource("ScrollBar.Pressed.Thumb", GetBrush(scrollBarPressedThumb));
            SetResource("ScrollBar.Pressed.Glyph", GetBrush(scrollBarPressedGlyph));
            SetResource("ScrollBar.Disabled.Background", GetBrush(scrollBarDisabledBackground));
            SetResource("ScrollBar.Disabled.Border", GetBrush(scrollBarDisabledBorder));
            SetResource("ScrollBar.Disabled.Glyph", GetBrush(scrollBarDisabledGlyph));
            SetResource("ScrollBar.Disabled.Thumb", GetBrush(scrollBarDisabledThumb));

            SetResource("TabItem.Static.Background", GetBrush(tabItemStaticBackground));
            SetResource("TabItem.Static.Border", GetBrush(tabItemStaticBorder));
            SetResource("TabItem.Selected.Background", GetBrush(tabItemSelectedBackground));
            SetResource("TabItem.Selected.Border", GetBrush(tabItemSelectedBorder));
            SetResource("TabItem.MouseOver.Background", GetBrush(tabItemMouseOverBackground));
            SetResource("TabItem.MouseOver.Border", GetBrush(tabItemMouseOverBorder));
            SetResource("TabItem.MouseOver.Background", GetBrush(tabItemMouseOverBackground));
            SetResource("TabItem.MouseOver.Border", GetBrush(tabItemMouseOverBorder));
        }

        private static Brush Gradient(string from, string to)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = GetColor(from), Offset = 0.0 },
                    new GradientStop { Color = GetColor(to), Offset = 0.0 },
                }
            };
            return brush;
        }

        private static void SetDefaultSystemColors()
        {
            SetResource(SystemColors.ActiveBorderBrushKey, SystemColors.ActiveBorderBrush);
            SetResource(SystemColors.ControlBrushKey, SystemColors.ControlBrush);
            SetResource(SystemColors.ControlTextBrushKey, SystemColors.ControlTextBrush);
            SetResource(SystemColors.WindowBrushKey, SystemColors.WindowBrush);
            SetResource(SystemColors.HighlightBrushKey, Brushes.LightSkyBlue);
            SetResource(SystemColors.InactiveSelectionHighlightBrushKey, SystemColors.InactiveSelectionHighlightBrush);
            SetResource(SystemColors.MenuHighlightBrushKey, SystemColors.MenuHighlightBrush);
            SetResource(SystemColors.MenuTextBrushKey, SystemColors.MenuTextBrush);
            SetResource(SystemColors.MenuBrushKey, SystemColors.MenuBrush);
            SetResource(SystemColors.MenuBarColorKey, SystemColors.MenuBarColor);
            SetResource(SystemColors.MenuHighlightColorKey, SystemColors.MenuHighlightColor);
            SetResource(SystemColors.MenuTextColorKey, SystemColors.MenuTextColor);
            SetResource(SystemColors.MenuColorKey, SystemColors.MenuColor);
            SetResource("Theme_InfoBarBackground", SystemColors.InfoBrush);
        }

        private static void SetResource(object key, object value)
        {
            Application.Current.Resources[key] = value;
        }
    }
}