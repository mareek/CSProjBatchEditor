using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using System.Xml.Linq;

namespace CSProjBatchEditor
{
    public enum EFrameworkVersion
    {
        Fx10,
        Fx11,
        Fx20,
        Fx30,
        Fx35,
        Fx40
    }
    
    public class AssemblyReference
    {

        //<Reference Include="Actaris.Data.DAC">
        //  <SpecificVersion>False</SpecificVersion>
        //  <Name>Actaris.Data.DAC</Name>
        //  <HintPath>..\..\..\Reference\Actaris.Data.DAC.dll</HintPath>
        //  <ExecutableExtension>.exe</ExecutableExtension>
        //  <RequiredTargetFramework>3.5</RequiredTargetFramework>
        //  <Private>True</Private>
        //  <FusionName>Actaris.Data.DAC</FusionName>
        //  <Aliases>???</Aliases>
        //</Reference>
        public string Name { get { return ReferenceName ?? AssemblyName; } }
        public string ReferenceName { get; private set; }
        public string AssemblyName { get; private set; }
        public string AssemblyCulture { get; private set; }
        public string AssemblyPublicKeyToken { get; private set; }
        public string AssemblyVersion { get; private set; }
        public ProcessorArchitecture? AssemblyProcessorArchitecture { get; private set; }
        public string HintPath { get; private set; }
        public bool SpecificVersion { get; private set; }
        public bool Private { get; private set; }
        public EFrameworkVersion? RequiredTargetFramework { get; private set; }
        public string ExecutableExtension { get; private set; }
        public string FusionName { get; private set; }
        public string Aliases { get; private set; }

        public AssemblyReference(XElement referenceElement)
        {
            ParseInclude(referenceElement);
            ReferenceName = referenceElement.GetChildValue("Name");
            HintPath = referenceElement.GetChildValue("HintPath");
            SpecificVersion = GetChildBooleanValue(referenceElement, true, "SpecificVersion");
            RequiredTargetFramework = TryParseFrameworkVersion(referenceElement.GetChildValue("RequiredTargetFramework"));
            Private = GetChildBooleanValue(referenceElement, false, "Private");
            ExecutableExtension = referenceElement.GetChildValue("ExecutableExtension");
            FusionName = referenceElement.GetChildValue("FusionName");
            Aliases = referenceElement.GetChildValue("Aliases");
        }

        private void ParseInclude(XElement referenceElement)
        {
            var includeAttributes = referenceElement.Attributes("Include");
            if (includeAttributes.Any())
            {
                var parts = includeAttributes.First().Value.Split(',');
                AssemblyName = parts[0];
                foreach (var part in parts.Skip(1).Select(p => p.Trim()))
                {
                    //Culture=neutral
                    //processorArchitecture=MSIL
                    //PublicKeyToken=d5e2aa3fffd7b98f
                    //Version=1.0.0.0
                    var subParts = part.Split('=');
                    switch (subParts[0])
                    {
                        case "Culture":
                            AssemblyCulture = subParts[1].Trim();
                            break;
                        case "processorArchitecture":
                            AssemblyProcessorArchitecture = (ProcessorArchitecture)Enum.Parse(typeof(ProcessorArchitecture), subParts[1].Trim(), true);
                            break;
                        case "PublicKeyToken":
                            AssemblyPublicKeyToken = subParts[1].Trim();
                            break;
                        case "Version":
                            AssemblyVersion = subParts[1].Trim();
                            break;
                    }
                }
            }
        }

        private static EFrameworkVersion? TryParseFrameworkVersion(string strVersion)
        {
            switch (strVersion)
            {
                case "1.0":
                    return EFrameworkVersion.Fx10;
                case "1.1":
                    return EFrameworkVersion.Fx11;
                case "2.0":
                    return EFrameworkVersion.Fx20;
                case "3.0":
                    return EFrameworkVersion.Fx30;
                case "3.5":
                    return EFrameworkVersion.Fx35;
                case "4.0":
                    return EFrameworkVersion.Fx40;
                default:
                    return null;
            }
        }

        private static bool GetChildBooleanValue(XElement parent, bool defaultVal, string childName)
        {
            bool parsedBool;
            if (bool.TryParse(parent.GetChildValue(childName), out parsedBool))
            {
                return parsedBool;
            }
            else
            {
                return defaultVal;
            }
        }
    }
}
