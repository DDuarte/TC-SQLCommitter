using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LibGit2Sharp;

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
            } while (!found);

            repositoryTextBox.Text = repositoryBrowserDialog.SelectedPath;
            FillAuthorsAutoComplete();
        }

        void FillAuthorsAutoComplete()
        {
            var repo = new Repository(repositoryTextBox.Text + "/.git");

            var nameSet = new HashSet<String>();

            foreach (var c in repo.Commits)
                nameSet.Add(c.Author.Name + " " + c.Author.Email);

            authorNameEmailTextBox.AutoCompleteCustomSource.AddRange(nameSet.ToArray());        }

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

            var repo = new Repository(repositoryPath);

            var authorNameEmail = authorNameEmailTextBox.Text;
            var idx = authorNameEmail.LastIndexOf(' ');
            if (idx == -1)
            {
                MessageBox.Show(@"Author text is not in a valid format: author_name<space>author_email", @"Error", MessageBoxButtons.OK);
                return;
            }

            var authorName = authorNameEmail.Substring(0, idx);
            var authorEmail = authorNameEmail.Substring(idx);

            var author = new Signature(authorName, authorEmail, DateTimeOffset.Now);
            var comitter = new Signature("Nay", "dnpd.dd@gmail.com", DateTimeOffset.Now);

            var fileName = repositoryTextBox.Text + filePathTextBox.Text + "/" + fileNameTextBox.Text;

            if (File.Exists(fileName))
            {
                MessageBox.Show(@"File already exists", @"Error", MessageBoxButtons.OK);
                return;
            }

            if (sqlScintilla.Text.LastIndexOf('\n') != sqlScintilla.Text.Length - 1)
                sqlScintilla.Text += '\n';

            TextWriter tw = new StreamWriter(fileName);
            tw.Write(sqlScintilla.Text);
            tw.Close();

            repo.Index.Stage(fileName);

            repo.Commit(commitTextBox.Text, author, comitter);

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

            if (files != null && files.Length == 1)
            {
                TextReader tr = new StreamReader(files[0]);
                sqlScintilla.Text = tr.ReadToEnd();
                tr.Close();
            }
        }

        private void TDBRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            repositoryTextBox.Text = @"D:\wow\tdb_434";
            filePathTextBox.Text = @"\updates\";

            FillAuthorsAutoComplete();
        }

        private void TCRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            repositoryTextBox.Text = @"D:\wow\TrinityCore";
            filePathTextBox.Text = @"\sql\updates\world\";

            FillAuthorsAutoComplete();
        }
    }
}
