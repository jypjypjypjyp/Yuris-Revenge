﻿using System;

namespace OpenRA.Mods.YR.UtilityCommands
{
    public class DisplayCurrentContentPath : IUtilityCommand
    {
        public string Name { get { return "--display-content-path"; } }

        bool IUtilityCommand.ValidateArguments(string[] args) { return true; }

        [Desc("", "Display current openra content path")]
        public void Run(Utility utility, string[] args)
        {
            Console.WriteLine("OpenRA Content Path: " + Platform.ResolvePath("^SupportDir|Content/"));
        }
    }
}
