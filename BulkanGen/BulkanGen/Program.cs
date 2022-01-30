using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace BulkanGen
{
    class Program
    {
        static void Main(string[] args)
        {
            string vkFile = "..\\..\\..\\..\\..\\KhronosRegistry\\vk.xml";
            string outputPath = "..\\..\\..\\..\\..\\Bulkan\\Bulkan\\src\\Generated";
            string projectNamespace = "Bulkan";

            var vulkanSpec = VulkanSpecification.FromFile(vkFile);

            var vulkanVersion = VulkanVersion.FromSpec(vulkanSpec, "AllVersions", vulkanSpec.Extensions.ToImmutableList());

            // Write Constants
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Constants.bf")))
            {
                file.WriteLine($"namespace {projectNamespace}");
                file.WriteLine("{");
                file.WriteLine("\tpublic extension VulkanNative");
                file.WriteLine("\t{");

                foreach (var constant in vulkanVersion.Constants)
                {
                    if (constant.Alias != null)
                    {
                        var refConstant = vulkanVersion.Constants.FirstOrDefault(c => c.Name == constant.Alias);
                        file.WriteLine($"\t\tpublic const {refConstant.Type.ToBeefType()} {constant.Name} = {refConstant.Name};");
                    }
                    else
                    {
                        file.WriteLine($"\t\tpublic const {constant.Type.ToBeefType()} {constant.Name} = {ConstantDefinition.NormalizeValue(constant.Value)};");
                    }
                }

                file.WriteLine("\t}");
                file.WriteLine("}");
            }

            // Function Pointers
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "FunctionPointers.bf")))
            {
                file.WriteLine("using System;\n");
                file.WriteLine($"namespace {projectNamespace}");
                file.WriteLine("{");

                foreach (var func in vulkanVersion.FuncPointers)
                {
                    file.Write($"\ttypealias {func.Name} = function {func.Type}(");
                    //file.Write($"\tpublic function {func.Type} {func.Name}(");
                    if (func.Parameters.Count > 0)
                    {
                        file.Write("\n");
                        string type, convertedType;

                        for (int p = 0; p < func.Parameters.Count; p++)
                        {
                            if (p > 0)
                                file.Write(",\n");

                            type = func.Parameters[p].Type;
                            var typeDef = vulkanSpec.TypeDefs.Find(t => t.Name == type);
                            if (typeDef != null)
                            {
                                vulkanSpec.BaseTypes.TryGetValue(typeDef.Type, out type);
                            }

                            convertedType = Helpers.ConvertBasicTypes(type);
                            if (convertedType == string.Empty)
                            {
                                convertedType = type;
                            }

                            file.Write($"\t\t{Helpers.GetPrettyEnumName(convertedType)} {Helpers.ValidatedName(func.Parameters[p].Name)}");
                        }
                    }
                    file.Write(");\n\n");
                }

                file.WriteLine("}");
            }

            // Enums
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Enums.bf")))
            {
                file.WriteLine("using System;\n");
                file.WriteLine($"namespace {projectNamespace}");
                file.WriteLine("{");

                foreach (var e in vulkanVersion.Enums)
                {
                    //if (e.Type == EnumType.Bitmask)
                    //    file.WriteLine("\t[Flags]");
                    file.WriteLine($"\t[AllowDuplicates]");
                    string underlyingType = "uint32";
                    if (Helpers.GetPrettyEnumName(e.Name).Equals("VkResult") 
                        || Helpers.GetPrettyEnumName(e.Name).Equals("VkFormatFeatureFlags2") 
                        || Helpers.GetPrettyEnumName(e.Name).Equals("VkFormatFeatureFlags2KHR") 
                        || Helpers.GetPrettyEnumName(e.Name).Equals("VkQueryResultStatusKHR"))
                    {
                        underlyingType = "int32";
                    }
                    file.WriteLine($"\tpublic enum {Helpers.GetPrettyEnumName(e.Name)} : {underlyingType}");
                    file.WriteLine("\t{");

                    if (!(e.Values.Exists(v => v.Value == 0)))
                    {
                        file.WriteLine("\t\tNone = 0,");
                    }

                    foreach (var member in e.Values)
                    {
                        file.WriteLine($"\t\t{member.Name} = {member.Value},");
                    }

                    file.WriteLine("\t}\n");

                }

                file.WriteLine("}");
            }

            // Unions
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Unions.bf")))
            {
                //file.WriteLine("using System.Runtime.InteropServices;\n");
                file.WriteLine($"using System;");
                file.WriteLine($"namespace {projectNamespace}");
                file.WriteLine("{");

                foreach (var union in vulkanVersion.Unions)
                {
                    file.WriteLine("\t[CRepr, Union]");
                    file.WriteLine($"\tpublic struct {union.Name}");
                    file.WriteLine("\t{");
                    foreach (var member in union.Members)
                    {
                        string csType = Helpers.ConvertToBeefType(member.Type, member.PointerLevel, vulkanSpec);

                        if (member.ElementCount > 1)
                        {
                            file.WriteLine($"\t\tpublic {csType}[{member.ElementCount}] {member.Name};");
                        }
                        else
                        {
                            file.WriteLine($"\t\tpublic {csType} {member.Name};");
                        }
                    }

                    file.WriteLine("\t}\n");
                }

                file.WriteLine("}\n");
            }

            // structs
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Structs.bf")))
            {
                file.WriteLine("using System;");
                //file.WriteLine("using System.Runtime.InteropServices;\n");
                file.WriteLine($"namespace {projectNamespace}");
                file.WriteLine("{");

                foreach (var structure in vulkanVersion.Structs)
                {
                    var useExplicitLayout = structure.Members.Any(s => s.ExplicityLayoutValue.HasValue == true);
                    int layoutValue = 0;
                    if (useExplicitLayout)
                    {
                        file.WriteLine("\t[CRepr]");
                    }
                    else
                    {
                        file.WriteLine("\t[CRepr]");
                    }
                    file.WriteLine($"\tpublic struct {structure.Name}");
                    file.WriteLine("\t{");


                    foreach (var member in structure.Members)
                    {
                        if (useExplicitLayout)
                        {
                            //file.WriteLine($"\t\t[FieldOffset({layoutValue})]");
                            layoutValue += Member.GetSizeInBytes(member, vulkanVersion);
                        }

                        string csType = Helpers.GetPrettyEnumName(Helpers.ConvertToBeefType(member.Type, member.PointerLevel, vulkanSpec));
                        string vkStructureType = "VkStructureType";

                        if (csType.Equals(vkStructureType, StringComparison.OrdinalIgnoreCase))
                        {
                            var vkStructureTypeEnum = vulkanVersion.Enums.FirstOrDefault(e => e.Name.Equals(vkStructureType, StringComparison.OrdinalIgnoreCase));

                            var sTypeCI = $"VkStructureType{structure.Name.Remove(0, 2)}";

                            var vkStructureValue = vkStructureTypeEnum.Values.FirstOrDefault(v => v.Name.Replace("_", "").Equals(sTypeCI, StringComparison.OrdinalIgnoreCase));
                            if (vkStructureValue != null)
                            {
                                file.WriteLine($"\t\tpublic {csType} {Helpers.ValidatedName(member.Name)} = .{vkStructureValue.Name};");
                                continue;
                            }
                        }

                        if (member.ElementCount > 1)
                        {
                            file.WriteLine($"\t\tpublic {csType}[{member.ElementCount}] {member.Name};");
                            //for (int i = 0; i < member.ElementCount; i++)
                            //{
                            //    file.WriteLine($"\t\tpublic {csType} {member.Name}_{i};");
                            //}
                        }
                        else if (member.ConstantValue != null)
                        {
                            var validConstant = vulkanVersion.Constants.FirstOrDefault(c => c.Name == member.ConstantValue);

                            if (Helpers.SupportFixed(csType))
                            {
                                file.WriteLine($"\t\tpublic {csType}[(int)VulkanNative.{validConstant.Name}] {Helpers.ValidatedName(member.Name)};");
                            }
                            else
                            {
                                int count = 0;

                                if (validConstant.Value == null)
                                {
                                    var alias = vulkanVersion.Constants.FirstOrDefault(c => c.Name == validConstant.Alias);
                                    count = int.Parse(alias.Value);
                                }
                                else
                                {
                                    count = int.Parse(validConstant.Value);
                                }
                                file.WriteLine($"\t\tpublic {csType}[{count}] {member.Name};");
                                /*
                                for (int i = 0; i < count; i++)
                                {
                                    file.WriteLine($"\t\tpublic {csType} {member.Name}_{i};");
                                }
                                */
                            }
                        }
                        else
                        {
                            file.WriteLine($"\t\tpublic {csType} {Helpers.ValidatedName(member.Name)};");
                        }
                    }

                    file.WriteLine("\t}\n");
                }

                file.WriteLine("}\n");
            }

            // Handles
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Handles.bf")))
            {
                file.WriteLine("using System;\n");
                file.WriteLine($"namespace {projectNamespace}");
                file.WriteLine("{");

                foreach (var handle in vulkanVersion.Handles)
                {
                    file.WriteLine("\t[CRepr]");
                    file.WriteLine($"\tpublic struct {handle.Name} : IEquatable<{handle.Name}>");
                    file.WriteLine("\t{");
                    string handleType = handle.Dispatchable ? "int" : "uint64";
                    string nullValue = handle.Dispatchable ? "0" : "0";

                    file.WriteLine($"\t\tpublic readonly {handleType} Handle;");

                    file.WriteLine($"\t\tpublic this({handleType} existingHandle) {{ Handle = existingHandle; }}");
                    file.WriteLine($"\t\tpublic static {handle.Name} Null => {handle.Name}({nullValue});");
                    file.WriteLine($"\t\tpublic static implicit operator {handle.Name}({handleType} handle) => {handle.Name}(handle);");
                    file.WriteLine($"\t\tpublic static bool operator ==({handle.Name} left, {handle.Name} right) => left.Handle == right.Handle;");
                    file.WriteLine($"\t\tpublic static bool operator !=({handle.Name} left, {handle.Name} right) => left.Handle != right.Handle;");
                    file.WriteLine($"\t\tpublic static bool operator ==({handle.Name} left, {handleType} right) => left.Handle == right;");
                    file.WriteLine($"\t\tpublic static bool operator !=({handle.Name} left, {handleType} right) => left.Handle != right;");
                    file.WriteLine($"\t\tpublic bool Equals({handle.Name} h) => Handle == h.Handle;");
                    //file.WriteLine($"\t\tpublic override bool Equals(object o) => o is {handle.Name} h && Equals(h);");
                    //file.WriteLine($"\t\tpublic override int GetHashCode() => Handle.GetHashCode();");
                    file.WriteLine("\t}\n");
                }

                file.WriteLine("}");
            }

            // Commands
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Commands.bf")))
            {
                file.WriteLine("using System;");
                file.WriteLine($"using internal {projectNamespace};");
                //file.WriteLine("using System.Runtime.InteropServices;\n");
                file.WriteLine($"namespace {projectNamespace}");
                file.WriteLine("{");


                file.WriteLine("\tpublic extension VulkanNative");
                file.WriteLine("\t{");

                foreach (var command in vulkanVersion.Commands)
                {
                    string convertedType = Helpers.ConvertToBeefType(command.Prototype.Type, 0, vulkanSpec);

                    //file.WriteLine("\t\t[UnmanagedFunctionPointer(CallConv)]");
                    //file.WriteLine($"\t\t[CallingConvention(VulkanNative.CallConv)]");
                    //public static extern Result vkCreateInstance(InstanceCreateInfo* pCreateInfo,AllocationCallbacks* pAllocator,Instance* pInstance);
                    //file.WriteLine($"\t\tprivate static function {convertedType} {command.Prototype.Name}Function({command.GetParametersSignature(vulkanSpec)});");
                    file.WriteLine($"\t\ttypealias {command.Prototype.Name}Function = function {convertedType}({command.GetParametersSignature(vulkanSpec)});");

                    // Delegate
                    //file.WriteLine($"\t\tprivate delegate {convertedType} {command.Prototype.Name}Delegate({command.GetParametersSignature(vulkanSpec)});");

                    // internal function
                    file.WriteLine($"\t\tprivate static {command.Prototype.Name}Function {command.Prototype.Name}_ptr;");

                    // public function
                    file.WriteLine($"\t\t[CallingConvention(VulkanNative.CallConv)]");
                    file.WriteLine($"\t\tpublic static {convertedType} {command.Prototype.Name}({command.GetParametersSignature(vulkanSpec)})");
                    file.WriteLine($"\t\t\t=> {command.Prototype.Name}_ptr({command.GetParametersSignature(vulkanSpec, useTypes: false)});\n");
                }

                file.WriteLine($"\t\tpublic static void LoadFuncionPointers(VkInstance instance = default)");
                file.WriteLine("\t\t{");
                file.WriteLine("\t\t\tif (instance != default)");
                file.WriteLine("\t\t\t{");
                file.WriteLine("\t\t\t\tNativeLib.mInstance = instance;");
                file.WriteLine("\t\t\t}");
                file.WriteLine();

                foreach (var command in vulkanVersion.Commands)
                {
                    file.WriteLine($"\t\t\tNativeLib.LoadFunction(\"{command.Prototype.Name}\",  out {command.Prototype.Name}_ptr);");
                }

                file.WriteLine("\t\t}");
                file.WriteLine("\t}");
                file.WriteLine("}");
            }
        }
    }
}
