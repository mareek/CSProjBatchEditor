using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CSProjBatchEditor
{
    public class Solution
    {
        private readonly FileInfo file;
        public FileInfo File { get { return file; } }
        private readonly List<Project> projects;
        public IEnumerable<Project> Projects { get { return projects.AsReadOnly(); } }

        public Solution(string filePath)
        {
            file = new FileInfo(filePath);
            projects = new List<Project>();
            var projectBlock = new List<string>();

            var streamReader = file.OpenText();
            while (!streamReader.EndOfStream)
            {
                var line = streamReader.ReadLine();
                if (line.StartsWith("Project(") && Project.IsRealProject(line, file.Directory))
                {
                    projectBlock = new List<string> { line };
                }
                else if (projectBlock.Any() && line == "EndProject")
                {
                    projectBlock.Add(line);
                    projects.Add(new Project(projectBlock[0], file.Directory));
                    projectBlock = new List<string>();
                }
                else if (projectBlock.Any())
                {
                    projectBlock.Add(line);                    
                }
            }
            projects = projects.OrderBy(p => p.Name).ToList();
        }
    }
}
