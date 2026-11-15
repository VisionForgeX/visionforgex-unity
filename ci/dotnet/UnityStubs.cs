// Just enough of UnityEngine for the plugin's Unity-free code to compile and be tested with plain .NET
// (ci/dotnet: runs on every push, with no Unity and no licence). The real Unity run is the GameCI job.
using System;

namespace UnityEngine
{
    public class Object { }
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => Activator.CreateInstance<T>();
    }
    public static class Resources { public static T Load<T>(string path) where T : Object => null; }
    public static class Application { public static bool isEditor => true; }
    public static class Debug
    {
        public static bool isDebugBuild => true;
        public static void Log(object m) => Console.WriteLine(m);
        public static void LogWarning(object m) => Console.WriteLine("warning: " + m);
        public static void LogError(object m) => Console.WriteLine("error: " + m);
    }
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
}
