using System.Diagnostics;

// The HUD compiles plugins in Debug, which marks the assembly DisableOptimizations.
// Declaring Debuggable without that flag lets the JIT optimize the plugin.
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]