using System;
using System.Collections.Generic;
using System.Reflection;
namespace AniDrag.EventBus.Utils
{
    public static class PreDefinedAssemlyUtil
    {
        /// <summary>
        /// Enumeration of Unity's predefined assembly names.
        /// </summary>
        public enum AssemblyType
        {
            AssemblyCSharp,
            AssemblyCSharpEditor,
            AssemblyCSharpFirstpass,
            AssemblyCSharpEditorFirstpass
        }


        /// <summary>
        /// Maps a Unity assembly name to its corresponding AssemblyType enum value.
        /// </summary>
        /// <param name="assemblyName">The full name of the assembly (e.g., "Assembly-CSharp").</param>
        /// <returns>The matching AssemblyType if recognized, otherwise null.</returns>
        static AssemblyType? GetAssemblyType(string assemblyName)
        {
            return assemblyName switch
            {
                "Assembly-CSharp" => AssemblyType.AssemblyCSharp,
                "Assembly-CSharp-Editor" => AssemblyType.AssemblyCSharpEditor,
                "Assembly-CSharp-firstpass" => AssemblyType.AssemblyCSharpFirstpass,
                "Assembly-CSharp-Editor-firstpass" => AssemblyType.AssemblyCSharpEditorFirstpass,
                _ => null
            };
        }


        /// <summary>
        /// Filters types from an assembly and adds those that derive from or implement the specified interface.
        /// </summary>
        /// <param name="assembly">Array of types from an assembly (typically from GetTypes()).</param>
        /// <param name="interfaceType">The interface type to check against.</param>
        /// <param name="types">The collection to which matching types will be added.</param>
        static void AddTypesFromAssembly(Type[] assembly, Type interfaceType, ICollection<Type> types)
        {
            if (assembly == null) return;
            for (int i = 0; i < assembly.Length; i++)
            {
                Type type = assembly[i];
                if(type != interfaceType && interfaceType.IsAssignableFrom(type))
                {
                    types.Add(type);
                }
            }
        }

        /// <summary>
        /// Retrieves all types from the runtime assemblies (Assembly-CSharp and Assembly-CSharp-firstpass)
        /// that implement or derive from the specified interface.
        /// </summary>
        /// <param name="interfaceType">The interface type to query (e.g., typeof(IEvent)).</param>
        /// <returns>A list of types that match the interface, excluding the interface itself. Returns an empty list if none are found.</returns>
        /// <remarks>
        /// This method ignores editor assemblies (those containing "-Editor" in the name). It ensures that
        /// only runtime code is scanned, which is useful for automatic registration or discovery systems.
        /// </remarks>
        public static List<Type> GetTypes(Type interfaceType)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Dictionary<AssemblyType, Type[]> assemblyTypeMap = new Dictionary<AssemblyType, Type[]>();
            List<Type> result = new List<Type>();

            // Fill dictionary only with assemblies that actually exist
            for (int i = 0; i < assemblies.Length; i++)
            {
                AssemblyType? assemblyType = GetAssemblyType(assemblies[i].GetName().Name);
                if (assemblyType != null)
                {
                    assemblyTypeMap[(AssemblyType)assemblyType] = assemblies[i].GetTypes();
                }
            }

            // Only process if the assembly exists in the map
            if (assemblyTypeMap.TryGetValue(AssemblyType.AssemblyCSharp, out Type[] csTypes))
            {
                AddTypesFromAssembly(csTypes, interfaceType, result);
            }

            if (assemblyTypeMap.TryGetValue(AssemblyType.AssemblyCSharpFirstpass, out Type[] firstpassTypes))
            {
                AddTypesFromAssembly(firstpassTypes, interfaceType, result);
            }

            return result;
        }
    }
}