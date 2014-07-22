using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using LibGit2Sharp;
using SharpConfig;

namespace SQLComitter
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var found = !String.IsNullOrEmpty(repositoryTextBox.Text);
            do
            {
                var res = repositoryBrowserDialog.ShowDialog();
                if (res == DialogResult.OK)
                {
                    var folderName = repositoryBrowserDialog.SelectedPath;
                    var dirs = Directory.GetDirectories(folderName);
                    found = dirs.Any(f => f.EndsWith(".git"));
                }
                else if (res == DialogResult.Cancel)
                    return;

            } while (!found);

            repositoryTextBox.Text = repositoryBrowserDialog.SelectedPath;

            FillAuthorsAutoComplete();
            FillSQLFileNameAutoComplete();
        }

        void FillAuthorsAutoComplete()
        {
            var ui = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Factory.StartNew(() => GetPreviousCommitAuthors(repositoryTextBox.Text))
                .ContinueWith(task =>
                {
                    if (task.Result != null)
                        authorNameEmailTextBox.AutoCompleteCustomSource.AddRange(task.Result.ToArray());
                    else
                        authorNameEmailTextBox.AutoCompleteMode = AutoCompleteMode.None;
                }, ui);
        }

        void FillSQLFileNameAutoComplete()
        {
            var ui = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Factory.StartNew(() => GetSQLFilesInDirectory(CombinePath(repositoryTextBox.Text, filePathTextBox.Text)))
                .ContinueWith(task =>
                {
                    if (task.Result != null)
                        fileNameTextBox.AutoCompleteCustomSource.AddRange(task.Result.ToArray());
                    else
                        fileNameTextBox.AutoCompleteMode = AutoCompleteMode.None;
                }, ui);

        }

        static string CombinePath(params string[] paths)
        {
            for (var i = 0; i < paths.Length; i++)
            {
                // Path.Combine doesn't combine paths starting with / or \
                if (paths[i].StartsWith("/") || paths[i].StartsWith("\\"))
                    paths[i] = paths[i].Remove(0, 1);

                if (i != paths.Length - 1)
                    if (!paths[i].EndsWith("/") && !paths[i].EndsWith("\\"))
                        paths[i] += "/";
            }

            return Path.Combine(paths);
        }

        static IEnumerable<string> GetSQLFilesInDirectory(string path)
        {
            try
            {
                var files = Directory.GetFiles(path, "*.sql", SearchOption.TopDirectoryOnly);
                return files.Select(Path.GetFileName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        static IEnumerable<string> GetPreviousCommitAuthors(string repoPath)
        {
            Repository repo;
            try
            {
                repo = new Repository(repoPath + "/.git");
            }
            catch (Exception)
            {
                return null;
            }

            var nameSet = new HashSet<String>();

            foreach (var c in repo.Commits)
                nameSet.Add(c.Author.Name + " " + c.Author.Email);

            return nameSet;
        }

        private static Signature SignatureFromString(string nameEmail)
        {
            var idx = nameEmail.LastIndexOf(' ');
            if (idx == -1)
                return null;

            var name = nameEmail.Substring(0, idx);
            var email = nameEmail.Substring(idx);

            var author = new Signature(name, email, DateTimeOffset.Now);
            return author;
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            commitTextBox.Clear();
            authorNameEmailTextBox.Clear();
            sqlScintilla.Text = "";
            fileNameTextBox.Clear();
        }

        private void commitButton_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrWhiteSpace(repositoryTextBox.Text))
            {
                MessageBox.Show(@"Repository can't be empty", @"Error", MessageBoxButtons.OK);
                return;
            }

            if (String.IsNullOrWhiteSpace(authorNameEmailTextBox.Text))
            {
                MessageBox.Show(@"Author can't be empty", @"Error", MessageBoxButtons.OK);
                return;
            }

            if (String.IsNullOrWhiteSpace(fileNameTextBox.Text))
            {
                MessageBox.Show(@"File name can't be empty", @"Error", MessageBoxButtons.OK);
                return;
            }

            if (String.IsNullOrWhiteSpace(filePathTextBox.Text))
            {
                MessageBox.Show(@"File path can't be empty", @"Error", MessageBoxButtons.OK);
                return;
            }

            if (String.IsNullOrWhiteSpace(sqlScintilla.Text))
            {
                MessageBox.Show(@"SQL content can't be empty", @"Error", MessageBoxButtons.OK);
                return;
            }

            var repositoryPath = repositoryTextBox.Text + "/.git";

            if (!Repository.IsValid(repositoryPath))
            {
                MessageBox.Show(@"Repository path is not a valid git repository", @"Error", MessageBoxButtons.OK);
                return;
            }

            Repository repo;
            try
            {
                repo = new Repository(repositoryPath);
            }
            catch (Exception)
            {
                MessageBox.Show(@"Could not open repository " + repositoryPath, @"Error", MessageBoxButtons.OK);
                return;
            }

            var author = SignatureFromString(authorNameEmailTextBox.Text);
            if (author == null)
            {
                MessageBox.Show(@"Author is not in a valid format: author_name<space>author_email", @"Error",
                    MessageBoxButtons.OK);
                return;
            }

            var committer = SignatureFromString(committerNameEmailTextBox.Text);
            if (committer == null)
            {
                MessageBox.Show(@"Committer is not in a valid format: committer_name<space>committer_email", @"Error",
                    MessageBoxButtons.OK);
                return;
            }

            var fileName = CombinePath(repositoryTextBox.Text, filePathTextBox.Text, fileNameTextBox.Text);

            if (File.Exists(fileName))
            {
                MessageBox.Show(@"File already exists", @"Error", MessageBoxButtons.OK);
                return;
            }

            if (sqlScintilla.Text.LastIndexOf('\n') != sqlScintilla.Text.Length - 1)
                sqlScintilla.Text += '\n';

            using (TextWriter tw = new StreamWriter(fileName))
            {
                tw.Write(sqlScintilla.Text);
            }

            repo.Index.Stage(fileName);

            try
            {
                repo.Commit(commitTextBox.Text, author, committer, new CommitOptions { PrettifyMessage = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(@"Commit failed: " + ex, @"Error", MessageBoxButtons.OK);
                return;
            }

            sqlScintilla.Text = "";
        }

        private void sqlScintilla_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
                e.Effect = DragDropEffects.All;
        }

        private void sqlScintilla_DragDrop(object sender, DragEventArgs e)
        {
            var files = (string[]) e.Data.GetData(DataFormats.FileDrop);

            if (files == null || files.Length != 1) // only read one file
                return;

            using (TextReader tr = new StreamReader(files[0]))
                sqlScintilla.Text = tr.ReadToEnd();
        }

        struct Profile
        {
            public string RepositoryPath { get; set; }
            public string SQLDirectory { get; set; }
            public string CommitterName { get; set; }
            public string CommitterEmail { get; set; }
        }

        private Config _config;

        private void MainForm_Load(object sender, EventArgs e)
        {
            const string ns = "SQLCommiter1.1";

            _config = new Config(ns, true);

            dynamic profiles;

            try
            {
                profiles = _config["profiles"];
            }
            catch (KeyNotFoundException)
            {
                profiles = new List<Profile>
                {
                    new Profile // default
                    {
                        RepositoryPath = @"C:/TrinityCore",
                        SQLDirectory = @"/sql/updates/world",
                        CommitterName = @"FillMeIn",
                        CommitterEmail = @"FillMe@In"
                    }
                };
            }

            foreach (var profile in profiles)
                reposDataGridView.Rows.Add(profile.RepositoryPath, profile.SQLDirectory, profile.CommitterName, profile.CommitterEmail);
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_config != null)
            {
                var profiles = new List<Profile>();

                foreach (DataGridViewRow row in reposDataGridView.Rows)
                {
                    var path = row.Cells[0].Value;
                    var sqlDir = row.Cells[1].Value;
                    var name = row.Cells[2].Value;
                    var email = row.Cells[3].Value;

                    if (path == null || sqlDir == null || name == null || email == null)
                        continue;

                    profiles.Add(new Profile
                    {
                        RepositoryPath = path.ToString(),
                        SQLDirectory = sqlDir.ToString(),
                        CommitterName = name.ToString(),
                        CommitterEmail = email.ToString()
                    });
                }

                _config["profiles"] = profiles;
                _config.Save();
            }
        }

        private void reposDataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            var senderGrid = (DataGridView)sender;

            if (senderGrid.Columns[e.ColumnIndex] is DataGridViewButtonColumn &&
                e.RowIndex >= 0)
            {
                var row = senderGrid.Rows[e.RowIndex];
                var path = row.Cells[0].Value;
                var sqlDir = row.Cells[1].Value;
                var name = row.Cells[2].Value;
                var email = row.Cells[3].Value;

                if (path == null || sqlDir == null || name == null || email == null)
                    return;

                repositoryTextBox.Text = path.ToString();
                filePathTextBox.Text = sqlDir.ToString();

                committerNameEmailTextBox.Text = name + @" " + email;

                FillAuthorsAutoComplete();
                FillSQLFileNameAutoComplete();
            }
        }
    }
}
