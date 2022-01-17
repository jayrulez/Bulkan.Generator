using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace BulkanGen
{
    public static class Helpers
    {
        public static string ToBeefType(this ConstantType type)
        {
            switch (type)
            {
                case ConstantType.UInt32:
                    return "uint32";
                case ConstantType.UInt64:
                    return "uint64";
                case ConstantType.Float32:
                    return "float";
                case ConstantType.String:
                    return "char8*"; //sed char8*
                default:
                    throw new InvalidOperationException("Invalid value");
            }
        }

        public static string ValidatedName(string name)
        {
            /*
            if (name == "object")
            {
                return "vkObject";
            }
            else if (name == "event")
            {
                return "vkEvent";
            }
            */

            if (name == "scope")
            {
                return "vkscope";
            }else if (name == "function")
            {
                return "vkfunction";
            }

            return name;
        }

        public static string GetPrettyEnumName(string value)
        {
            int start;
            if((start = value.IndexOf("bit", StringComparison.OrdinalIgnoreCase)) != -1)
            {
                return value.Remove(start, 3);
            }

            return value;
        }

        public static uint GetTypeSize(string type)
        {
            if (type == "char" || type == "uint8_t")
                return 1;
            else if (type == "uint16_t" || type == "int16_t")
                return 2;

            // uint32_t, uint64_t, int32_t, int64_t, int64_t*, size_t, DWORD
            return 4;
        }

        public static string ConvertToBeefType(string type, int pointerlevel, VulkanSpecification spec)
        {
            string memberType = type;

            if (IsVoidPtr(memberType))
                return "void*";//"IntPtr"; //sed void*

            if (type.StartsWith("PFN"))
                return "void*";//return type;// "IntPtr"; //sed void*

            if (type.StartsWith('"'))
            {
                return "char8*"; //sed char8*
            }

            string result = ConvertBasicTypes(memberType);
            if (result == string.Empty)
            {
                if (spec.Alias.TryGetValue(memberType, out string alias))
                {
                    memberType = alias;
                }

                spec.BaseTypes.TryGetValue(memberType, out string baseType);
                if (baseType != null)
                {
                    result = ConvertBasicTypes(baseType);
                }
                else
                {
                    var typeDef = spec.TypeDefs.Find(t => t.Name == memberType);
                    if (typeDef != null)
                    {
                        if (typeDef.Requires != null)
                        {
                            result = typeDef.Requires;
                        }
                        else
                        {
                            spec.BaseTypes.TryGetValue(typeDef.Type, out baseType);
                            if (baseType != null)
                            {
                                result = ConvertBasicTypes(baseType);
                            }
                        }
                    }
                    else
                    {
                        result = memberType;
                    }
                }
            }

            if (pointerlevel > 0)
            {
                for (int i = 0; i < pointerlevel; i++)
                {
                    result += "*";
                }
            }

            return result;
        }

        public static string ConvertBasicTypes(string type)
        {
            switch (type)
            {
                case "int8_t":
                    return "int8";
                case "int8_t*":
                    return "int8*";
                case "uint8_t":
                    return "uint8";
                case "char":
                    return "char8";
                case "uint8_t*":
                    return "uint8*";
                case "char*":
                    return "char8*"; //sed char8*
                case "uint16_t":
                    return "uint16";
                case "uint16_t*":
                    return "uint16*";
                case "int16_t":
                    return "int16";
                case "int16_t*":
                    return "int16*";
                case "uint32_t":
                case "DWORD":
                    return "uint32";
                case "uint32_t*":
                    return "uint32*";
                case "uint64_t":
                    return "uint64";
                case "uint64_t*":
                    return "uint64*";
                case "int32_t":
                    return "int32";
                case "int32_t*":
                    return "int32*";
                case "int64_t":
                    return "int64";
                case "int64_t*":
                    return "int64*";
                case "size_t":
                    return "uint";
                case "float":
                    return "float";
                case "float*":
                    return "float*";
                case "double":
                    return "double";
                case "double*":
                    return "double*";
                case "void":
                    return "void";
                case "VkBool32":
                    return "VkBool32";
                case "VkExtent2D":
                    return "VkExtent2D";
                case "VkOffset2D":
                    return "VkOffset2D";
                case "VkRect2D":
                    return "VkRect2D";
                default:
                    return string.Empty;
            }
        }

        public static bool SupportFixed(string type)
        {
            switch (type)
            {
                case "bool":
                case "uint8":
                case "int16":
                case "int32":
                case "int64":
                case "char8":
                case "int8":
                case "uint16":
                case "uint32":
                case "uint64":
                case "float":
                case "double":
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsVoidPtr(string type)
        {
            switch (type)
            {
                case "Display":
                case "VisualID":
                case "Window":
                case "RROutput":
                case "wl_display":
                case "wl_surface":
                case "HINSTANCE":
                case "HWND":
                case "HMONITOR":
                case "HANDLE":
                case "SECURITY_ATTRIBUTES":
                case "DWORD":
                case "LPCWSTR":
                case "xcb_connection_t":
                case "xcb_visualid_t":
                case "xcb_window_t":
                case "IDirectFB":
                case "IDirectFBSurface":
                case "zx_handle_t":
                case "GgpStreamDescriptor":
                case "GgpFrameToken":
                case "CAMetalLayer":
                case "AHardwareBuffer":
                case "ANativeWindow":
                // NV extension
                case "_screen_context":
                case "_screen_window":
                case "StdVideoH264ProfileIdc":
                case "StdVideoH264PictureParameterSet":
                case "StdVideoH264SequenceParameterSet":
                case "StdVideoDecodeH264PictureInfo":
                case "StdVideoDecodeH264ReferenceInfo":
                case "StdVideoDecodeH264Mvc":
                case "StdVideoH265SequenceParameterSet":
                case "StdVideoH265PictureParameterSet":
                case "StdVideoDecodeH265PictureInfo":
                case "StdVideoDecodeH265ReferenceInfo":
                case "StdVideoEncodeH264PictureInfo":
                case "StdVideoEncodeH264SliceHeader":
                case "StdVideoH265ProfileIdc":

                    // jayrulez
                case "StdVideoEncodeH265PictureInfo":
                case "StdVideoEncodeH265ReferenceInfo":
                case "StdVideoEncodeH265ReferenceModifications":
                case "StdVideoEncodeH265SliceHeader":
                case "StdVideoH265VideoParameterSet":
                    return true;
                default:
                    return false;
            }
        }
    }
}