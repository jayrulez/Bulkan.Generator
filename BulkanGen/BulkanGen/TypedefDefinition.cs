﻿using System.Xml.Linq;

namespace BulkanGen
{
    public class TypedefDefinition
    {
        public string Name;
        public string Requires;
        public string Type;

        public static TypedefDefinition FromXML(XElement elem)
        {
            TypedefDefinition typeDef = new TypedefDefinition();
            typeDef.Name = elem.Element("name").Value;
            typeDef.Requires = elem.Attribute("requires")?.Value;
            typeDef.Type = elem.Element("type").Value;

            return typeDef;
        }
    }
}
