// Project-wide usings -- makes cross-namespace access transparent.
// The Editor namespaces only exist under #if TOOLS (all their types are #if TOOLS wrapped), so we
// conditionally global-use them too; otherwise the game build would fail with "namespace not found".

global using Cadastile.Tilemap;

#if TOOLS
global using Cadastile.Editor.Brush;
global using Cadastile.Editor.Brush.Tools;
global using Cadastile.Editor.Overlay;
#endif
