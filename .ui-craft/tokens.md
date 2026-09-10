# Native resource contract

- Primitive colors, accent, radii, typography, focus, hover, pressed states, and motion come from WinUI's XamlControlsResources.
- Semantic surfaces use ApplicationPageBackgroundThemeBrush and LayerFillColorDefaultBrush; text uses TextFillColorPrimaryBrush and TextFillColorSecondaryBrush; separators use DividerStrokeColorDefaultBrush.
- ThemeResource references follow system light/dark and high-contrast changes. Do not assign Application.RequestedTheme or force a theme on dialogs.
- Component styles live in App.xaml: ShellIconButtonStyle and SidebarItemButtonStyle. Spacing uses 4/8/12/16/24/32 effective pixels. Body uses 14, supporting text 12, headings 20/28.
- Native ContentDialog owns modal elevation, corner radius, focus trapping, and dismissal; no bespoke overlay system.
- Sidebar widths and responsive thresholds are defined in SidebarLayoutState, with a visible keyboard-focusable resize handle and a 48-pixel collapsed rail.
- Initial visual intent: restrained Fluent, medium list density, native motion only, conventional two-pane composition. The continuous draggable sidebar edge is the distinctive interaction.
