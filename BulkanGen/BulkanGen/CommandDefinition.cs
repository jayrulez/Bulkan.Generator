using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BulkanGen
{
    public class CommandDefinition
    {
        public Proto Prototype;
        public List<Param> Parameters = new List<Param>();
        public string[] Queues;
        public string RenderPass;
        public string[] CmdBufferLevel;
        public string Pipeline;
        public string[] SuccessCodes;
        public string[] ErrorCodes;
        public string Comment;
        public bool IsInstanceCommand;

        public static CommandDefinition FromXML(XElement elem)
        {
            CommandDefinition command = new CommandDefinition();

            command.SuccessCodes = elem.Attribute("successcodes")?.Value.Split(',');
            command.ErrorCodes = elem.Attribute("errorcodes")?.Value.Split(',');
            command.Queues = elem.Attribute("queues")?.Value.Split(',');
            command.RenderPass = elem.Attribute("renderpass")?.Value;
            command.Pipeline = elem.Attribute("pipeline")?.Value;
            command.CmdBufferLevel = elem.Attribute("cmdbufferlevel")?.Value.Split(',');
            command.Comment = elem.Attribute("comment")?.Value;

            var proto = elem.Element("proto");

            if (proto != null)
            {
                command.Prototype = new Proto
                {
                    Name = proto.Element("name").Value,
                    Type = proto.Element("type").Value,
                };
            }

            var parameters = elem.Elements("param");
            var names = new List<string>();
            foreach (var param in parameters)
            {
                var name = param.Element("name").Value;
                if (names.Contains(name))
                    continue;

                names.Add(name);

                var parsed = Param.FromXML(param);
                if(parsed.Name == "instance" && parsed.Type == "VkInstance")
                    command.IsInstanceCommand = true;
                command.Parameters.Add(parsed);
            }

            return command;
        }

        public string GetParametersSignature(VulkanSpecification spec, bool useTypes = true)
        {
            StringBuilder signature = new StringBuilder();
            foreach (var p in Parameters)
            {
                // Avoid Vulkan Safety Critical
                if (Helpers.IsVKSC(p.Api))
                {
                    continue;
                }

                string convertedType = Helpers.GetPrettyEnumName(Helpers.ConvertToBeefType(p.Type, p.PointerLevel, spec));
                string convertedName = Helpers.ValidatedName(p.Name);

                if (useTypes)
                {
                    if (p.IsStaticArray)
                        signature.Append($"{convertedType}[{p.StaticArrayLength}] ");
                    else
                        signature.Append($"{convertedType} ");

                }

                signature.Append($"{convertedName}, ");
            }

            signature.Length -= 2;

            return signature.ToString();
        }
    }

    public class Proto
    {
        public string Type;
        public string Name;
    }

    public class Param
    {
        private static Regex _arraySubscriptRegex = new Regex(@"(\[\d+\])$", RegexOptions.IgnoreCase);
        private static Regex _numberRegex = new Regex(@"(\d+)", RegexOptions.IgnoreCase);

        public string Type;
        public string Name;
        public string Api;
        public int PointerLevel;
        public bool IsOptional;
        public string Externsync;
        public string Len;
        public bool IsNoautovalidity;
        public bool IsStaticArray;
        public int StaticArrayLength;

        internal static Param FromXML(XElement elem)
        {
            Param p = new Param();
            p.Type = elem.Element("type").Value;
            p.Name = elem.Element("name").Value;
            p.Api = elem.Attribute("api")?.Value;
            p.Externsync = elem.Attribute("externsync")?.Value;
            p.Len = elem.Attribute("len")?.Value;
            p.IsNoautovalidity = elem.Attribute("noautovalidity")?.Value == "true";
            p.IsOptional = elem.Attribute("optional")?.Value == "true";

            if (elem.Value.Contains($"{p.Type}**") || elem.Value.Contains($"{p.Type}* const*"))
            {
                p.PointerLevel = 2;
            }
            else if (elem.Value.Contains($"{p.Type}*"))
            {
                p.PointerLevel = 1;
            }

            Match arrayMatch = _arraySubscriptRegex.Match(elem.Value);
            if (arrayMatch.Success)
            {
                Match arraySizeMatch = _numberRegex.Match(arrayMatch.Captures[0].Value);

                if (arraySizeMatch.Success)
                {
                    if (int.TryParse(arraySizeMatch.Captures[0].Value, out p.StaticArrayLength))
                    {
                        p.IsStaticArray = true;
                    }
                }
            }

            return p;
        }
    }
}
