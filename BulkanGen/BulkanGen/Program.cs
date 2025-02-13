using System;
using System.Collections.Generic;
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
                file.WriteLine($"namespace {projectNamespace};");
                //file.WriteLine("{");
                file.WriteLine("");
                file.WriteLine("public extension VulkanNative");
                file.WriteLine("{");

                foreach (var constant in vulkanVersion.Constants)
                {
                    if (constant.Alias != null)
                    {
                        var refConstant = vulkanVersion.Constants.FirstOrDefault(c => c.Name == constant.Alias);
                        file.WriteLine($"\tpublic const {refConstant.Type.ToBeefType()} {constant.Name} = {refConstant.Name};");
                    }
                    else
                    {
                        file.WriteLine($"\tpublic const {constant.Type.ToBeefType()} {constant.Name} = {ConstantDefinition.NormalizeValue(constant.Value)};");
                    }
                }

                file.WriteLine("}");
                //file.WriteLine("}");
                file.WriteLine("");
            }

            // Function Pointers
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "FunctionPointers.bf")))
            {
                file.WriteLine("using System;\n");
                file.WriteLine($"namespace {projectNamespace};");
                //file.WriteLine("{");
                file.WriteLine("");

                foreach (var func in vulkanVersion.FuncPointers)
                {
                    file.Write($"public typealias {func.Name} = function {func.Type}(");
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

                            file.Write($"\t{Helpers.GetPrettyEnumName(convertedType)} {Helpers.ValidatedName(func.Parameters[p].Name)}");
                        }
                    }
                    file.Write(");\n\n");
                }

                //file.WriteLine("}");
                file.WriteLine("");
            }

            // Enums
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Enums.bf")))
            {
                file.WriteLine("using System;\n");
                file.WriteLine($"namespace {projectNamespace};");
                //file.WriteLine("{");
                file.WriteLine("");

                foreach (var e in vulkanVersion.Enums)
                {
                    var typeDefs = vulkanSpec.TypeDefs;
                    var baseTypes = vulkanSpec.BaseTypes;

                    string underlyingType = "int32";

                    var prettyName = Helpers.GetPrettyEnumName(e.Name);

                    var typeDef = vulkanSpec.TypeDefs.FirstOrDefault(td => td.Name == prettyName);

                    if (typeDef != null)
                    {
                        switch (typeDef.Type)
                        {
                            case "VkSampleMask":
                                underlyingType = "uint32";
                                break;
                            case "VkBool32":
                                underlyingType = "uint32";
                                break;
                            case "VkFlags":
                                underlyingType = "uint32";
                                break;
                            case "VkFlags64":
                                underlyingType = "uint64";
                                break;
                            case "VkDeviceSize":
                                underlyingType = "uint64";
                                break;
                            case "VkDeviceAddress":
                                underlyingType = "uint64";
                                break;

                            default:
                                throw new Exception("Unexpected typedef, see vk.xml from Khronos registry.");
                        }
                    }

                    var prettyMembers = new StringWriter();

                    //if (e.Type == EnumType.Bitmask)
                    //    file.WriteLine("[Flags]");
                    file.WriteLine($"[AllowDuplicates]");
                    //if (prettyName.Equals("VkResult")
                    //    || prettyName.Equals("VkFormatFeatureFlags2")
                    //    || prettyName.Equals("VkFormatFeatureFlags2KHR")
                    //    || prettyName.Equals("VkQueryResultStatusKHR")
                    //    || prettyName.Equals("VkOpacityMicromapSpecialIndexEXT"))
                    //{
                    //    underlyingType = "int32";
                    //}
                    file.WriteLine($"public enum {prettyName} : {underlyingType}");
                    file.WriteLine("{");

                    if (!(e.Values.Exists(v => v.Value == 0)))
                    {
                        file.WriteLine("\tNone = 0,");
                    }
                    foreach (var member in e.Values)
                    {
                        if (string.IsNullOrEmpty(member.HexValueString))
                        {
                            // {(member.Value < 0 && underlyingType != "int32" ? "(" + underlyingType + ")" : "")}
                            file.WriteLine($"\t{member.Name} = {member.Value},");
                        }
                        else
                        {
                            file.WriteLine($"\t{member.Name} = {member.HexValueString},");
                        }
                        var prettyMemberName = Helpers.GetPrettyEnumValue(e.Name, member.Name);
                        prettyMembers.WriteLine($"\t{prettyMemberName} = .{member.Name},");
                    }

                    file.WriteLine("\t// Pretty names");
                    file.WriteLine(prettyMembers);

                    file.WriteLine("}\n");

                }

                //file.WriteLine("}");
                file.WriteLine("");
            }

            // Unions
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Unions.bf")))
            {
                //file.WriteLine("using System.Runtime.InteropServices;\n");
                file.WriteLine($"using System;");
                file.WriteLine($"namespace {projectNamespace};");
                //file.WriteLine("{");
                file.WriteLine("");

                foreach (var union in vulkanVersion.Unions)
                {
                    file.WriteLine("[CRepr, Union]");
                    file.WriteLine($"public struct {union.Name}");
                    file.WriteLine("{");
                    foreach (var member in union.Members)
                    {
                        string csType = Helpers.ConvertToBeefType(member.Type, member.PointerLevel, vulkanSpec);

                        if (member.ElementCount > 1)
                        {
                            file.WriteLine($"\tpublic {csType}[{member.ElementCount}] {member.Name};");
                        }
                        else
                        {
                            file.WriteLine($"\tpublic {csType} {member.Name};");
                        }
                    }

                    file.WriteLine("}\n");
                }

                //file.WriteLine("}\n");
                file.WriteLine("\n");
            }

            // structs
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Structs.bf")))
            {
                file.WriteLine("using System;");
                //file.WriteLine("using System.Runtime.InteropServices;\n");
                file.WriteLine($"namespace {projectNamespace};");
                //file.WriteLine("{");
                file.WriteLine("");

                foreach (var structure in vulkanVersion.Structs)
                {
                    var methodsStream = new StringWriter();

                    var useExplicitLayout = structure.Members.Any(s => s.ExplicityLayoutValue.HasValue == true);
                    int layoutValue = 0;
                    if (useExplicitLayout)
                    {
                        file.WriteLine("[CRepr]");
                    }
                    else
                    {
                        file.WriteLine("[CRepr]");
                    }
                    file.WriteLine($"public struct {structure.Name}");
                    file.WriteLine("{");



                    foreach (var member in structure.Members)
                    {
                        // Avoid duplicate members from Vulkan Safety Critical
                        if (Helpers.IsVKSC(member.Api))
                        {
                            continue;
                        }

                        if (useExplicitLayout)
                        {
                            //file.WriteLine($"\t[FieldOffset({layoutValue})]");
                            layoutValue += Member.GetSizeInBytes(member, vulkanVersion);
                        }

                        string bfType = Helpers.GetPrettyEnumName(Helpers.ConvertToBeefType(member.Type, member.PointerLevel, vulkanSpec));
                        if (member.ElementCount > 1)
                        {
                            bfType = $"{bfType}[{member.ElementCount}]";
                        }
                        else if (member.ConstantValue != null)
                        {
                            var validConstant = vulkanVersion.Constants.FirstOrDefault(c => c.Name == member.ConstantValue);

                            if (Helpers.SupportFixed(bfType))
                            {
                                bfType = $"{bfType}[(int)VulkanNative.{validConstant.Name}]";
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
                                bfType = $"{bfType}[{count}]";
                            }
                        }

                        methodsStream.WriteLine("");
                        string setMethodName = Helpers.ValidatedName(member.Name);
                        setMethodName = "set" + char.ToUpper(Helpers.ValidatedName(member.Name)[0]) + Helpers.ValidatedName(member.Name).Substring(1);
                        methodsStream.Write($"\tpublic ref Self {setMethodName}({bfType} @{Helpers.ValidatedName(member.Name)}) mut {{ {Helpers.ValidatedName(member.Name)} = @{Helpers.ValidatedName(member.Name)};  return ref this; }}");

                        string vkStructureType = "VkStructureType";
                        if (bfType.Equals(vkStructureType, StringComparison.OrdinalIgnoreCase))
                        {
                            var vkStructureTypeEnum = vulkanVersion.Enums.FirstOrDefault(e => e.Name.Equals(vkStructureType, StringComparison.OrdinalIgnoreCase));

                            var sTypeCI = $"VkStructureType{structure.Name.Remove(0, 2)}";

                            var vkStructureValue = vkStructureTypeEnum.Values.FirstOrDefault(v => v.Name.Replace("_", "").Equals(sTypeCI, StringComparison.OrdinalIgnoreCase));
                            if (vkStructureValue != null)
                            {
                                file.WriteLine($"\tpublic {bfType} {Helpers.ValidatedName(member.Name)} = .{vkStructureValue.Name};");

                                continue;
                            }
                        }

                        if (Helpers.ValidatedName(member.Name) == "pNext")
                            file.WriteLine($"\tpublic {bfType} {Helpers.ValidatedName(member.Name)} = null;");
                        else
                            file.WriteLine($"\tpublic {bfType} {Helpers.ValidatedName(member.Name)};");
                    }
                    methodsStream.WriteLine();
                    file.Write(methodsStream);

                    file.WriteLine("}\n");
                }

                //file.WriteLine("}\n");
                file.WriteLine("\n");
            }

            // Handles
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Handles.bf")))
            {
                file.WriteLine("using System;\n");
                file.WriteLine($"namespace {projectNamespace};");
                //file.WriteLine("{");
                file.WriteLine("");

                foreach (var handle in vulkanVersion.Handles)
                {
                    file.WriteLine("[CRepr]");
                    file.WriteLine($"public struct {handle.Name} : IEquatable<{handle.Name}>, IHashable");
                    file.WriteLine("{");
                    string handleType = handle.Dispatchable ? "int" : "uint64";
                    string nullValue = handle.Dispatchable ? "0" : "0";

                    file.WriteLine($"\tpublic readonly {handleType} Handle;");

                    file.WriteLine($"\tpublic this({handleType} existingHandle) {{ Handle = existingHandle; }}");

                    if (handleType != "int")
                        file.WriteLine($"\tpublic this(void* existingHandle) {{ Handle = ({handleType})(int)existingHandle; }}");
                    else
                        file.WriteLine($"\tpublic this(void* existingHandle) {{ Handle = ({handleType})existingHandle; }}");

                    file.WriteLine($"\tpublic static Self Null => Self({nullValue});");
                    file.WriteLine($"\tpublic static implicit operator Self({handleType} handle) => Self(handle);");
                    file.WriteLine($"\tpublic static implicit operator {handleType}(Self self) => self.Handle;");

                    if (handleType != "int")
                    {
                        file.WriteLine($"\tpublic static implicit operator Self(void* handle) => Self(({handleType})(int)handle);");
                        file.WriteLine($"\tpublic static implicit operator void*(Self self) => (void*)(int)self.Handle;");
                    }
                    else
                    {
                        file.WriteLine($"\tpublic static implicit operator Self(void* handle) => Self(({handleType})handle);");
                        file.WriteLine($"\tpublic static implicit operator void*(Self self) => (void*)({handleType})self.Handle;");
                    }

                    file.WriteLine($"\tpublic static bool operator ==(Self left, Self right) => left.Handle == right.Handle;");
                    file.WriteLine($"\tpublic static bool operator !=(Self left, Self right) => left.Handle != right.Handle;");
                    file.WriteLine($"\tpublic static bool operator ==(Self left, {handleType} right) => left.Handle == right;");
                    file.WriteLine($"\tpublic static bool operator !=(Self left, {handleType} right) => left.Handle != right;");
                    file.WriteLine($"\tpublic bool Equals(Self h) => Handle == h.Handle;");
                    file.WriteLine($"");
                    file.WriteLine($"\tpublic int GetHashCode() {{ return (.)Handle; }}");
                    //file.WriteLine($"\tpublic override bool Equals(object o) => o is {handle.Name} h && Equals(h);");
                    //file.WriteLine($"\tpublic override int GetHashCode() => Handle.GetHashCode();");
                    file.WriteLine("}\n");
                }

                //file.WriteLine("}");
            }

            // Commands
            using (StreamWriter file = File.CreateText(Path.Combine(outputPath, "Commands.bf")))
            {
                file.WriteLine("using System;");
                file.WriteLine("using System.Collections;");
                file.WriteLine($"using internal {projectNamespace};");
                //file.WriteLine("using System.Runtime.InteropServices;\n");
                file.WriteLine($"namespace {projectNamespace};");
                //file.WriteLine("{");
                file.WriteLine("");


                file.WriteLine("public extension VulkanNative");
                file.WriteLine("{");

                var commandDictionary = new List<string>();
                var instanceCommands = new List<string>();
                foreach (var command in vulkanVersion.Commands)
                {
                    if (command.IsInstanceCommand)
                        instanceCommands.Add(command.Prototype.Name);

                    string convertedType = Helpers.ConvertToBeefType(command.Prototype.Type, 0, vulkanSpec);

                    //file.WriteLine("\t[UnmanagedFunctionPointer(CallConv)]");
                    //file.WriteLine($"\t[CallingConvention(VulkanNative.CallConv)]");
                    //public static extern Result vkCreateInstance(InstanceCreateInfo* pCreateInfo,AllocationCallbacks* pAllocator,Instance* pInstance);
                    //file.WriteLine($"\tprivate static function {convertedType} {command.Prototype.Name}Function({command.GetParametersSignature(vulkanSpec)});");
                    file.WriteLine($"\tpublic typealias {command.Prototype.Name}Function = function {convertedType}({command.GetParametersSignature(vulkanSpec)});");

                    // Delegate
                    //file.WriteLine($"\tprivate delegate {convertedType} {command.Prototype.Name}Delegate({command.GetParametersSignature(vulkanSpec)});");

                    // internal function
                    file.WriteLine($"\tprivate static {command.Prototype.Name}Function {command.Prototype.Name}_ptr;");

                    // public function
                    file.WriteLine($"\t[CallingConvention(VulkanNative.CallConv)]");
                    file.WriteLine($"\tpublic static {convertedType} {command.Prototype.Name}({command.GetParametersSignature(vulkanSpec)})");
                    file.WriteLine($"\t\t=> {command.Prototype.Name}_ptr({command.GetParametersSignature(vulkanSpec, useTypes: false)});\n");

                    commandDictionary.Add($"\tcase \"{command.Prototype.Name}\":");
                    commandDictionary.Add($"\t\tLoadFunction(\"{command.Prototype.Name}\", out {command.Prototype.Name}_ptr, instance, invokeErrorCallback);");
                    commandDictionary.Add($"\t\tif({command.Prototype.Name}_ptr == null)");
                    commandDictionary.Add($"\t\t\treturn .Err;");
                    commandDictionary.Add($"\t\tbreak;");
                    commandDictionary.Add("");
                }

                Func<String, String> getGroup = (name) =>
                {
                    if (name.Contains("Xlib"))
                        return "Xlib";
                    if (name.Contains("Xcb"))
                        return "Xcb";
                    if (name.Contains("Wayland"))
                        return "Wayland";
                    if (name.Contains("Android"))
                        return "Android";
                    if (name.Contains("Win32"))
                        return "Win32";
                    if (name.Contains("GGP"))
                        return "GGP";
                    if (name.Contains("NN"))
                        return "NN";
                    if (name.Contains("MVK"))
                        return "MVK";
                    if (name.Contains("FUCHSIA"))
                        return "FUCHSIA";
                    if (name.Contains("QNX"))
                        return "QNX";
                    if (name.Contains("Metal"))
                        return "Metal";
                    if (name.Contains("DirectFB"))
                        return "DirectFB";
                    if (name.Contains("Headless"))
                        return "Headless";
                    return "Agnostic";
                };

                var instanceCommandGroups = instanceCommands.GroupBy(c => getGroup(c)).ToList();

                file.WriteLine();

                file.WriteLine("\tprivate static List<(InstanceFunctionFlags Flags, String Name)> sKnownInstanceCommands = new .()");
                file.WriteLine("\t{");
                foreach (var group in instanceCommandGroups)
                {
                    foreach (var instanceCommand in group)
                    {
                        // Exclude these as they are only available if validation layer is enabled
                        if (new List<string>() {
                            "vkCreateDebugReportCallbackEXT",
                            "vkDestroyDebugReportCallbackEXT",
                            "vkDebugReportMessageEXT",
                            "vkCreateDebugUtilsMessengerEXT",
                            "vkDestroyDebugUtilsMessengerEXT",
                            "vkSubmitDebugUtilsMessageEXT",
                        }.Contains(instanceCommand))
                            continue;
                        file.WriteLine($"\t\t(.{group.Key}, \"{instanceCommand}\"),");
                    }
                }
                file.WriteLine("\t} ~ delete _;");
                file.WriteLine();

                if (commandDictionary.Count > 0)
                {
                    file.WriteLine("\tpublic static Result<void> LoadFunction(StringView name, VkInstance? instance = null, bool invokeErrorCallback = true)");
                    file.WriteLine("\t{");

                    file.WriteLine("\t\tif(mLoadedFunctions.Contains(scope .(name)))");
                    file.WriteLine("\t\t{");
                    file.WriteLine("\t\t\treturn .Ok;");
                    file.WriteLine("\t\t}");
                    file.WriteLine();

                    file.WriteLine("\t\tswitch (name) {");

                    foreach (var commandItem in commandDictionary)
                    {
                        if (string.IsNullOrEmpty(commandItem))
                            file.WriteLine();
                        else
                            file.WriteLine($"\t{commandItem}");
                    }

                    file.WriteLine($"\t\tdefault:");
                    file.WriteLine($"\t\t\tRuntime.FatalError(scope $\"Unknown function name '{{name}}'.\");");
                    file.WriteLine("\t\t}");
                    file.WriteLine("\t\treturn .Ok;");
                    file.WriteLine("\t}");
                    file.WriteLine();
                }

                file.WriteLine($"\tprivate static void LoadAllFuncions(VkInstance? instance = null, List<String> excludeFunctions = null)");
                file.WriteLine("\t{");
                foreach (var command in vulkanVersion.Commands)
                {
                    file.WriteLine($"\t\tif(excludeFunctions == null || !excludeFunctions.Contains(\"{command.Prototype.Name}\"))");
                    file.WriteLine($"\t\t\tLoadFunction(\"{command.Prototype.Name}\", instance).IgnoreError();");
                    file.WriteLine();
                }

                file.WriteLine("\t}");

                file.WriteLine("}");
                //file.WriteLine("}");
            }
        }
    }
}
