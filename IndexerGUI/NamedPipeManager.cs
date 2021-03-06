// This file is the part of the Indexer++ project.
// Copyright (C) 2016 Anna Krykora <krykoraanna@gmail.com>. All rights reserved.
// Use of this source code is governed by a MIT-style license that can be found in the LICENSE file.

﻿using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Indexer
{
    internal class NamedPipeManager
    {
        private static string pipeName;
        private static Dispatcher dispatcher;

        public NamedPipeManager(Dispatcher STADispatcher)
        {
            dispatcher = STADispatcher;
            pipeName = Helper.GetNamePrefix();
        }

        public void StartClient(string input)
        {
            using (var client = new NamedPipeClientStream(pipeName))
            {
                client.Connect();
                using (var writer = new StreamWriter(client))
                {
                    writer.WriteLine(input);
                }
            }
        }

        public void StartServer()
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    using (var server = new NamedPipeServerStream(pipeName))
                    {
                        server.WaitForConnection();
                        string searchDirPath;
                        using (var reader = new StreamReader(server))
                        {
                            searchDirPath = reader.ReadLine();
                        }
                        dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                Helper.MakeIndexerMainWndVisible();
                                Helper.SetMainWndDirPathFilter(searchDirPath);
                            }),
                            DispatcherPriority.Normal);
                    }
                }
            });
        }
    }
}
