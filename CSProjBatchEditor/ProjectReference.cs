using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace CSProjBatchEditor
{
    public class ProjectReference
    {
    //<ProjectReference Include="..\Actaris.ERH.BO\Actaris.ERH.BO.csproj">
    //  <Project>{58D7F7D4-6CE8-40E9-85B0-F9353CDDDCE6}</Project>
    //  <Name>Actaris.ERH.BO</Name>
    //</ProjectReference>

        public string ProjectName { get; private set; }
        public Guid ProjectGuid { get; private set; }

        private readonly FileInfo projectFile;
        public Project Project { get { return new Project(projectFile); } }

        public ProjectReference(XElement projectReferenceElement, string refPath)
        {
            projectFile = new FileInfo(Path.Combine(refPath, projectReferenceElement.Attribute("Include").Value));
            ProjectName = projectReferenceElement.GetChildValue("Name");
            ProjectGuid = new Guid(projectReferenceElement.GetChildValue("Project"));
        }
    }
}
