// C# 6: Profiler recording off (the 2026-11-20 16:20 Editor crash was in the Profiler's frame-data reader during a test run).
var was = UnityEditorInternal.ProfilerDriver.enabled;
UnityEditorInternal.ProfilerDriver.enabled = false;
UnityEngine.Profiling.Profiler.enabled = false;
return "Profiler recording was " + was + ", now " + UnityEditorInternal.ProfilerDriver.enabled + "; playing " + UnityEditor.EditorApplication.isPlaying;
