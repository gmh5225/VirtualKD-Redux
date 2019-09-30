﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VirtualBoxIntegration
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public List<MachineWrapper> Machines {get;set;}

        static string MyDir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = this;

            _VirtualBox = new VirtualBox.VirtualBox();
            lblVersion.Content = _VirtualBox.Version;
            if (int.Parse(_VirtualBox.Version.Split('.')[0]) < 5)
            {
                throw new Exception("VirtualBox older than 5.0 detected. Please install VirtualBox 5.0 or later to use this version of VirtualKD.");
            }

            var is64Bit = App.Is64Bit();
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Oracle\VirtualBox");
            string dir;
            if (key != null)
                dir = key.GetValue("InstallDir") as string;
            else
                dir = Environment.GetEnvironmentVariable(is64Bit ? "ProgramW6432" : "ProgramFiles") + "\\Oracle\\VirtualBox";
            if (dir == null || !Directory.Exists(dir))
                throw new Exception("VirtualBox directory not found");
            VirtualBoxPath = dir.TrimEnd('\\');

            Refresh();
        }
        const string VirtualKDConfigEntry = "VBoxInternal/Devices/VirtualKD/0/Config/Path";


        public class MachineWrapper : INotifyPropertyChanged
        {
            private VirtualBox.IMachine _Machine;
            private MainWindow _Window;

            public MachineWrapper(MainWindow w, VirtualBox.IMachine m)
            {
                _Machine = m;
                _Window = w;
                EnableCommand = new EnableDisableCommand(this, true);
                DisableCommand = new EnableDisableCommand(this, false);
            }

            public string Name
            {
                get
                {
                    return _Machine.Name;
                }
            }


            public class EnableDisableCommand : ICommand
            {
                private MachineWrapper _Wrapper;
                public EnableDisableCommand(MachineWrapper wrp, bool enable)
                {
                    _Enable = enable;
                    _Wrapper = wrp;
                }

                public bool CanExecute(object parameter)
                {
                    return _Enable != _Wrapper.Integrated;
                }

                public event EventHandler CanExecuteChanged;
                private bool _Enable;

                public void Execute(object parameter)
                {
                    if (App.IsVirtualBoxRunning())
                    {
                        MessageBox.Show("Please close ALL VirtualBox instances before changing the settings.", "VirtualBoxIntegration", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ProcessStartInfo info = new ProcessStartInfo(System.IO.Path.Combine(_Wrapper._Window.VirtualBoxPath, "VBoxManage.exe"), "setextradata \"" + _Wrapper._Machine.Name + "\" " + VirtualKDConfigEntry) { CreateNoWindow = true, UseShellExecute = false };
                    if (_Enable)
                    {
                        info.Arguments += " \"" + MyDir + "\"";
                        App.CheckKDClientPermissions(MyDir);
                    }

                    var proc = Process.Start(info);
                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        MessageBox.Show("Failed to update machine properties", "VirtualBoxIntegration", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    _Wrapper.InvalidateStatus();
               }

                public void InvalidateStatus()
                {
                    if (CanExecuteChanged != null)
                        CanExecuteChanged(this, EventArgs.Empty);
                }
            }

            public EnableDisableCommand EnableCommand { get; set; }
            public EnableDisableCommand DisableCommand { get; set; }

            public bool Integrated
            {
                get
                {
                    return _Machine.GetExtraData(VirtualKDConfigEntry) == MyDir;
                }
            }

            public string Status
            {
                get
                {
                    return Integrated ? "Enabled" : "Disabled";
                }
            }


            internal void InvalidateStatus()
            {
                EnableCommand.InvalidateStatus();
                DisableCommand.InvalidateStatus();
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("Status"));
                    PropertyChanged(this, new PropertyChangedEventArgs("Integrated"));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void Refresh()
        {
            Machines = _VirtualBox.Machines.Cast<VirtualBox.IMachine>().Select(m => new MachineWrapper(this, m)).ToList();
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs("Machines"));
        }
        

        public event PropertyChangedEventHandler PropertyChanged;
        private VirtualBox.VirtualBox _VirtualBox;
        private string VirtualBoxPath;

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Process.Start(System.IO.Path.Combine(VirtualBoxPath, "VirtualBox.exe"));
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
