// C# 6, Edit mode: import changed scripts and ask Unity to compile (call compile_state.cs after).
if (UnityEditor.EditorApplication.isPlaying) return "PLAYING: stop first";
UnityEditor.AssetDatabase.Refresh();
UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
return "refresh + compile requested; compiling " + UnityEditor.EditorApplication.isCompiling;
