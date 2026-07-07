// Proje geneli using'ler — cakraz-namespace erisimini seffaflastirir.
// Editor namespace'leri yalnizca #if TOOLS altinda var oldugundan (tum tipleri
// #if TOOLS sarili), onlari da kosullu global-use ediyoruz; yoksa oyun build'inde
// "namespace bulunamadi" hatasi olur.


#if TOOLS
global using Cadastile.Editor;
global using Cadastile.Editor.Brush;
global using Cadastile.Editor.Overlay;
#endif
