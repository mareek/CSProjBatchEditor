using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;

namespace CSProjBatchEditor
{
    public class Project
    {
        private const string NAMESPACE = "{http://schemas.microsoft.com/developer/msbuild/2003}";
        public string Name { get; private set; }
        private readonly FileInfo file;
        public FileInfo ProjectFile { get { return file; } }
        private readonly XDocument xDoc;

        public IEnumerable<FileInfo> Files
        {
            get
            {
                return (from itemGroup in xDoc.Descendants(NAMESPACE + "ItemGroup")
                        from element in itemGroup.Descendants()
                        where element.Attributes("Include").Any()
                              && !(new[] { "Reference", "BootstrapperPackage", "AppDesigner" })
                                   .Contains(element.Name.LocalName)
                        select new FileInfo(Path.Combine(file.DirectoryName, element.Attribute("Include").Value))
                       ).Union(new[] { file });
            }
        }

        private IEnumerable<XElement> OutputPaths
        {
            get
            {
                return from project in xDoc.Elements(NAMESPACE + "Project")
                       from propertyGroup in project.Elements(NAMESPACE + "PropertyGroup")
                       from outputPath in propertyGroup.Elements(NAMESPACE + "OutputPath")
                       select outputPath;
            }
        }

        private List<AssemblyReference> _references;
        public IEnumerable<AssemblyReference> References
        {
            get
            {
                if(_references==null)
                {
                    _references = (from project in xDoc.Elements(NAMESPACE + "Project")
                                  from itemGroup in project.Elements(NAMESPACE + "ItemGroup")
                                  from reference in itemGroup.Elements(NAMESPACE + "Reference")
                                  select new AssemblyReference(reference)).ToList();
                }
                return _references.AsReadOnly();
            }
        }

        private IEnumerable<XElement> HintPaths
        {
            get
            {
                return from project in xDoc.Elements(NAMESPACE + "Project")
                       from itemGroup in project.Elements(NAMESPACE + "ItemGroup")
                       from reference in itemGroup.Elements(NAMESPACE + "Reference")
                       from hintPath in reference.Elements(NAMESPACE + "HintPath")
                       select hintPath;
            }
        }

        private List<ProjectReference> _projectReferences;
        public IEnumerable<ProjectReference> ProjectReferences
        {
            get
            {
                if (_projectReferences == null)
                {
                    _projectReferences = (from project in xDoc.Elements(NAMESPACE + "Project")
                                          from itemGroup in project.Elements(NAMESPACE + "ItemGroup")
                                          from projectReference in itemGroup.Elements(NAMESPACE + "ProjectReference")
                                          select new ProjectReference(projectReference, file.Directory.FullName)).ToList();
                }
                return _projectReferences.AsReadOnly();
            }
        }

        public IEnumerable<string> BuildConfs
        {
            get
            {
                var conditions = (from project in xDoc.Elements(NAMESPACE + "Project")
                                  from propertyGroup in project.Elements(NAMESPACE + "PropertyGroup")
                                  where propertyGroup.Attributes("Condition").Any()
                                  select propertyGroup.Attribute("Condition").Value
                                  ).Distinct();
                return (from condition in conditions
                        where condition.Contains('|')
                        select condition.Split(new[] { "==" }, StringSplitOptions.None)[1].Trim().Split('|')[0].Replace("'", "")
                        ).Distinct();
            }
        }

        public string TargetPlatform
        {
            get
            {
                return (from project in xDoc.Elements(NAMESPACE + "Project")
                        from propertyGroup in project.Elements(NAMESPACE + "PropertyGroup")
                        from platformName in propertyGroup.Elements(NAMESPACE + "PlatformFamilyName")
                        select platformName.Value).FirstOrDefault();
            }
        }

        public bool CompactFramework
        {
            get
            {
                return new[] { "WindowsCE", "PocketPC" }.Contains(TargetPlatform);
            }
        }
        
        public Project(string solutionLine, DirectoryInfo solutionDirectory)
        {
            string path;
            string name;
            ParseSolutionLine(solutionLine, solutionDirectory, out path, out name);
            
            Name = name;
            file = new FileInfo(path);
            using (var stream = file.OpenText())
            {
                xDoc = XDocument.Load(stream);
            }
        }

        public Project(FileInfo file)
        {
            this.file = file;
            Name = Path.GetFileNameWithoutExtension(file.Name);
            using (var stream = file.OpenText())
            {
                xDoc = XDocument.Load(stream);
            }
        }

        public static bool IsRealProject(string solutionLine, DirectoryInfo solutionDirectory)
        {
            string path;
            string name;
            ParseSolutionLine(solutionLine, solutionDirectory, out path, out name);
            return File.Exists(path);
        }

        private static void ParseSolutionLine(string solutionLine, DirectoryInfo solutionDirectory, out string path, out string name)
        {
            //Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Actaris.Data.BO.Interfaces.Desktop", "..\..\MCN_SoftwareReusables\DotNet\Actaris\Actaris.Data.BO.Interfaces\Actaris.Data.BO.Interfaces.Desktop.csproj", "{6168A5D9-6AED-4841-B89A-C97280A5D2DA}"
            var splittedLine = solutionLine.Split('=')[1].Split(',');
            name = splittedLine[0].Replace("\"", "").Trim();
            var relativeFilePath = splittedLine[1].Replace("\"", "").Trim();
            path = Path.GetFullPath(Path.Combine(solutionDirectory.FullName, relativeFilePath));
        }

        public void SetReferencePath(DirectoryInfo newPath)
        {
            var relativePath = file.Directory.GetRelativePath(newPath);
            UpdateOutputPath(relativePath);
            UpdateReferencesHintPath(relativePath);
            Save();
        }

        private void UpdateOutputPath(string newPath)
        {
            foreach (var outputPath in OutputPaths)
            {
                outputPath.Value = newPath;
            }
        }

        private void UpdateReferencesHintPath(string newPath)
        {
            foreach (var hintPath in HintPaths)
            {
                if (hintPath.Value.Contains(@"C:\Actaris.Deliverables.DotNet2005"))
                {
                    var oldPath = hintPath.Value;
                    var fileName = oldPath.Split('\\').Last();
                    hintPath.Value = Path.Combine(newPath, fileName);
                }
            }
        }

        private void Save()
        {
            if ((file.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                file.Attributes = file.Attributes & ~FileAttributes.ReadOnly;
                file.Refresh();
            }
            xDoc.Save(file.FullName);
        }

        public IEnumerable<FileInfo> GetFilesInProjectDirTree()
        {
            var binDirPath = Path.Combine(file.DirectoryName, "bin");
            var objDirPath = Path.Combine(file.DirectoryName, "obj");
            return from fsFile in GetAllFilesFromDir(file.Directory)
                   where !fsFile.FullName.Contains(objDirPath)
                         && !fsFile.FullName.Contains(binDirPath)
                   select fsFile;
        }

        public bool IsReferencing(Project project)
        {
            return References.Where(r => r.Name == project.Name).Any()
                   || ProjectReferences.Where(pr => pr.ProjectName == project.Name).Any();
        }
        
        public bool IsReferencedByAny(IEnumerable<Project> projects)
        {
            return projects.Where(p => p.IsReferencing(this)).Any();
        }

        private static IEnumerable<FileInfo> GetAllFilesFromDir(DirectoryInfo dir)
        {
            return GetAllFilesFromDir(dir, f => true);
        }
        private static IEnumerable<FileInfo> GetAllFilesFromDir(DirectoryInfo dir, Func<FileInfo, bool> predicate)
        {
            return dir.GetFiles().Union(from subDir in dir.GetDirectories()
                                        from file in GetAllFilesFromDir(subDir)
                                        select file).Where(predicate);
        }

        public static IEnumerable<FileInfo> GetNonProjectFiles(IEnumerable<Project> projects)
        {
            var iStrComp = StringComparer.InvariantCultureIgnoreCase;
            var projectFiles = (from project in projects
                                from file in project.Files
                                orderby file.FullName
                                select file.FullName).Distinct(iStrComp);
            var fsFiles = (from project in projects
                           from file in project.GetFilesInProjectDirTree()
                           orderby file.FullName
                           select file.FullName).Distinct(iStrComp);

            return from filePath in fsFiles.Except(projectFiles, iStrComp)
                   select new FileInfo(filePath);
        }

        public static IEnumerable<Project> GetAllProjectsFromDir(DirectoryInfo dir)
        {
            Project project = null;

            return from file in GetAllFilesFromDir(dir, f => f.Extension == ".csproj")
                   where TryBuildProject(file, out project)
                   select project;
        }

        public static bool TryBuildProject(FileInfo projectFile, out Project project)
        {
            try
            {
                project = new Project(projectFile);
                return true;
            }
            catch
            {
                project = null;
                return false;
            }
        }
    }
}
