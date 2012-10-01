using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Path = System.IO.Path;
using System.Xml.Linq;
using System.Xml;
using System.Text;

namespace CSProjBatchEditor
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<Solution> solutions;
        private DirectoryInfo searchDir;
        private DirectoryInfo referenceDir;
        private List<Project> projects;

        private DirectoryInfo BaseDir
        {
            get
            {
                return (searchDir != null) ? searchDir : solutions.First().File.Directory;
            }
        }

        private readonly DispatcherTimer waitingTimer;

        public MainWindow()
        {
            InitializeComponent();
            waitingTimer = new DispatcherTimer();
            waitingTimer.Interval = TimeSpan.FromSeconds(0.5);
            waitingTimer.Tick += AnimateWaiting;
        }

        private void BrowseSolutionButton_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new Microsoft.Win32.OpenFileDialog
                                 {
                                     FileName = "",
                                     DefaultExt = ".sln",
                                     Filter = "Solution (*.sln)|*.sln",
                                     Multiselect = true
                                 };

            if (fileDialog.ShowDialog(this) ?? false)
            {
                projects = null;
                searchDir = null;
                txtSolutionPath.Text = string.Join(" ; ", fileDialog.FileNames);
                solutions = fileDialog.FileNames.Select(f => new Solution(f)).ToList();
                UpdateGrid();
            }
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog()
            {
                ShowNewFolderButton = true,
                Description = "Search path for .csproj"
            })
            {
                if (searchDir != null)
                {
                    dlg.SelectedPath = searchDir.FullName;
                }
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    projects = null;
                    solutions = null;
                    searchDir = new DirectoryInfo(dlg.SelectedPath);
                    txtSolutionPath.Text = searchDir.FullName;
                    UpdateGrid();
                }
            }
        }

        private void LoadProjects()
        {
            IEnumerable<Project> ienumProj = null;
            if (solutions != null && solutions.Any())
            {
                ienumProj = solutions.SelectMany(s => s.Projects);
            }
            else if (searchDir != null)
            {
                ienumProj = Project.GetAllProjectsFromDir(searchDir);
            }


            if (ienumProj == null)
            {
                projects = null;
            }
            else
            {
                projects = (from proj in ienumProj
                            orderby proj.ProjectFile.Directory.FullName
                            orderby proj.ProjectFile.FullName
                            select proj).ToList();
            }
        }

        private void UpdateGrid()
        {
            EnterWaitMode();
            ListProjects.ItemsSource = null;
            ThreadPool.QueueUserWorkItem(stateInfo =>
                {
                    LoadProjects();
                    EndUpdateGrid();
                });
        }

        private void EndUpdateGrid()
        {
            Dispatcher.BeginInvoke((Action)(() =>
                {
                    ListProjects.ItemsSource = projects.ToList();
                    UpdateOutputPathButton.IsEnabled = true;
                    GetNonProjectFiles.IsEnabled = true;
                    GetReferencesPaths.IsEnabled = true;
                    GetReferences.IsEnabled = true;
                    GetProjectsPath.IsEnabled = true;
                    GetAllFilesPath.IsEnabled = true;
                    GetTopLevelProjects.IsEnabled = true;
                    GetProjectReferences.IsEnabled = true;
                    FindMisversionnedReferences.IsEnabled = true;
                    FindUntranslatedXaml.IsEnabled = true;
                    SearchReference.IsEnabled = true;
                    SearchText.IsEnabled = true;
                    LeaveWaitMode();
                }));
        }

        private void EnterWaitMode()
        {
            EnterWaitMode("Loading");
        }
        private void EnterWaitMode(string label)
        {
            WaitingLabel.Text = label;
            WaitingPoints.Text = ".  ";
            waitingTimer.Start();
            Cursor = Cursors.Wait;
            GrayOverlay.Visibility = Visibility.Visible;
        }

        private void AnimateWaiting(object sender, EventArgs e)
        {
            var nbPoints = (WaitingPoints.Text.Trim().Length + 1) % 4;
            var nbSpaces = 3 - nbPoints;
            WaitingPoints.Text = new string('.', nbPoints) + new string(' ', nbSpaces);
        }

        private void LeaveWaitMode()
        {
            waitingTimer.Stop();
            GrayOverlay.Visibility = Visibility.Collapsed;
            Cursor = Cursors.Arrow;
        }

        private void UpdateOutputPathButton_Click(object sender, RoutedEventArgs e)
        {
            DirectoryInfo outputDirectory = null;
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog()
                                 {
                                     ShowNewFolderButton = true,
                                     SelectedPath = (this.referenceDir ?? this.solutions.First().File.Directory.Parent.Parent).FullName,
                                     Description = "Output path"
                                 })
            {
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    outputDirectory = new DirectoryInfo(dlg.SelectedPath);
                }
            }
            if (outputDirectory != null)
            {
                referenceDir = outputDirectory;
                foreach (var project in projects)
                {
                    project.SetReferencePath(outputDirectory);
                }
                MessageBox.Show(this, "Output path updated successfully", "Succes", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void GetNonProjectFiles_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new Microsoft.Win32.SaveFileDialog()
                                 {
                                     FileName = "UnusedFiles.txt"
                                 };

            if (fileDialog.ShowDialog(this) ?? false)
            {
                SaveNonProjectFiles(new FileInfo(fileDialog.FileName));
            }
        }

        private void SaveNonProjectFiles(FileInfo saveFile)
        {
            EnterWaitMode("Analyzing");
            ThreadPool.QueueUserWorkItem(stateInfo =>
                {
                    var nonProjectFiles = Project.GetNonProjectFiles(projects);
                    using (var saveStream = saveFile.CreateText())
                    {
                        foreach (var fileInfo in nonProjectFiles.Where(f => f.Extension == ".cs"))
                        {
                            saveStream.WriteLine(fileInfo.FullName.Replace(searchDir.FullName, ""));
                        }
                    }
                    EndSaveNonProjectFiles();
                });
        }

        private void EndSaveNonProjectFiles()
        {
            Dispatcher.BeginInvoke((Action)(LeaveWaitMode));
        }

        private void GetReferencesPaths_Click(object sender, RoutedEventArgs e)
        {
            var includes = from project in projects
                           from reference in project.References
                           where reference.HintPath != null
                           select Path.GetDirectoryName(reference.HintPath);

            MessageBox.Show(includes.Distinct().OrderBy(n => n).Aggregate("", (res, cur) => res + "\r\n" + cur));
        }

        private void GetProjectsPath_Click(object sender, RoutedEventArgs e)
        {
            var paths = (from project in projects
                         let path = project.ProjectFile.Directory.FullName
                         orderby path
                         select path
                        ).Distinct();
            MessageBox.Show(string.Join("\r\n", paths.ToArray()));
        }

        private void GetAllFilesPath_Click(object sender, RoutedEventArgs e)
        {
            var projectDirs = from project in projects
                              select project.ProjectFile.Directory;
            var fileDirs = from project in projects
                           from file in project.Files
                           select file.Directory;
            var dirs = projectDirs.Union(fileDirs)
                                  .Select(d => d.FullName)
                                  .Distinct()
                                  .Select(p => new DirectoryInfo(p)).ToList();
            var paths = (from dir in dirs
                         where !dir.IsSubDirectoryOfAny(dirs)
                         orderby dir.FullName
                         select dir.FullName).Distinct();
            MessageBox.Show(string.Join("\r\n", paths.ToArray()));
        }

        private void GetReferences_Click(object sender, RoutedEventArgs e)
        {
            var includes = (from project in projects
                            from reference in project.References
                            where reference.HintPath != null
                            orderby reference.HintPath
                            select reference.HintPath).Distinct();
            MessageBox.Show(string.Join("\r\n", includes.ToArray()));
        }

        private void GetTopLevelProjects_Click(object sender, RoutedEventArgs e)
        {
            var nonTestProjects = projects.Where(p => !p.Name.EndsWith(".tests", true, CultureInfo.InvariantCulture)
                                                      && !p.Name.EndsWith(".test", true, CultureInfo.InvariantCulture));
            var topLevelProjects = from project in nonTestProjects
                                   where !project.IsReferencedByAny(nonTestProjects)
                                   orderby project.Name
                                   select project.Name;
            MessageBox.Show(string.Join("\r\n", topLevelProjects.ToArray()));
        }

        private void SearchReference_Click(object sender, RoutedEventArgs e)
        {
            SearchReferenceNameInProjects(SearchText.Text);
        }

        private void SearchText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchReferenceNameInProjects(SearchText.Text);
            }
        }

        private void SearchReferenceNameInProjects(string referenceName)
        {
            var result = (from project in projects
                          from reference in project.References
                          where reference.Name.IndexOf(referenceName, StringComparison.InvariantCultureIgnoreCase) >= 0
                          select new { project, reference }
                         ).Distinct();
            var strResult = string.Join("\r\n", result.Select(r => r.project.Name + " : " + r.reference.Name).ToArray());
            strResult = string.IsNullOrEmpty(strResult) ? "[rien]" : strResult;
            MessageBox.Show(strResult);
        }

        private void GetProjectReferences_Click(object sender, RoutedEventArgs e)
        {
            var includes = (from project in projects
                            from projectReference in project.ProjectReferences
                            orderby project.Name, projectReference.ProjectName
                            select project.Name + " - " + projectReference.ProjectName).Distinct();
            MessageBox.Show(string.Join("\r\n", includes.ToArray()));
        }

        private void FindMisversionnedReferences_Click(object sender, RoutedEventArgs e)
        {
            var duplicateReferences = from project in projects
                                      from ref1 in project.References
                                      from ref2 in project.References
                                      where ref1.Name == ref2.Name
                                         && ref1.AssemblyVersion != ref2.AssemblyVersion
                                      orderby ref1.Name
                                      select ref1.Name + " : " + ref1.AssemblyVersion;// +"/" + ref2.AssemblyVersion;

            MessageBox.Show(string.Join("\r\n", duplicateReferences.Distinct().ToArray()));
        }

        private void FindUntranslatedXaml_Click(object sender, RoutedEventArgs e)
        {
            Func<FileInfo, XDocument> LoadXamlFile = xamlFile => { using (var stream = xamlFile.OpenRead()) return XDocument.Load(stream, LoadOptions.SetLineInfo); };

            Func<string, bool> IsStringUntranslated = str => !string.IsNullOrWhiteSpace(str) && !str.Contains("{") && str.Any(c => char.IsLetter(c));

            var untranslatedElements = (from project in projects
                                        from file in project.Files
                                        where file.Name.EndsWith(".xaml", StringComparison.InvariantCultureIgnoreCase)
                                        let xamlDoc = LoadXamlFile(file)
                                        from element in xamlDoc.Root.Descendants()
                                        where GetDisplayedTextsOfElement(element).Any(t => IsStringUntranslated(t))
                                        select new { File = file, Element = element })
                                       .ToLookup(ue => ue.File, ue => ue.Element);

            const string indent = "    ";
            var report = new StringBuilder();
            foreach (var fileGroup in untranslatedElements)
            {
                var file = fileGroup.Key;
                report.AppendLine(Path.Combine(BaseDir.GetRelativePath(file.Directory), file.Name));

                foreach (var element in fileGroup)
                {
                    report.Append(indent);
                    report.Append(((IXmlLineInfo)element).HasLineInfo() ? ((IXmlLineInfo)element).LineNumber : -1);
                    report.Append(" : <");
                    report.Append(element.Name.LocalName);
                    var xName = element.GetAttributeValue("Name");
                    if (!string.IsNullOrEmpty(xName))
                    {
                        report.AppendFormat(" x:Name=\"{0}\"", xName);
                    }
                    report.AppendFormat(" UntranslatedText=\"{0}\"", string.Join(", ", GetDisplayedTextsOfElement(element).Where(t => IsStringUntranslated(t))));
                    report.Append(" />");
                    report.AppendLine();
                }
                report.AppendLine();
            }

            MessageBox.Show(this, report.ToString());
        }

        private IEnumerable<string> GetDisplayedTextsOfElement(XElement xamlElement)
        {
            switch (xamlElement.Name.LocalName)
            {
                case "TextBlock":
                    if (xamlElement.HasElements)
                    {
                        yield return "";
                    }
                    else
                    {
                        yield return xamlElement.GetAttributeValue("Text");
                        yield return xamlElement.Value;
                    }
                    break;
                case "Label":
                case "Button":
                case "ToggleButton":
                case "CheckBox":
                case "RadioButton":
                case "ToolTip":
                    if (xamlElement.HasElements)
                    {
                        yield return "";
                    }
                    else
                    {
                        yield return xamlElement.GetAttributeValue("Content");
                        yield return xamlElement.Value;
                    }
                    break;
                case "TabItem":
                case "GroupBox":
                case "Expander":
                case "MenuItem":
                case "TreeViewItem":
                    yield return xamlElement.GetAttributeValue("Header");
                    break;
                case "Window":
                    yield return xamlElement.GetAttributeValue("Title");
                    break;
                default:
                    yield return "";
                    break;
            }

            yield return xamlElement.GetAttributeValue("ToolTip");
        }
    }
}
