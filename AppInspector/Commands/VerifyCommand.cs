﻿// Copyright (C) Microsoft. All rights reserved.
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using RulesEngine;


namespace Microsoft.AppInspector
{
    /// <summary>
    /// Wraps rulesengine verify for ruleset
    /// </summary>
    public class VerifyRulesCommand : ICommand
   {
        public enum ExitCode
        {
            Verified = 0,
            NotVerified = 1,
            CriticalError = 2
        }

        List<string> _rulePaths = new List<string>();
        private string _arg_customRulesPath;
        private bool _arg_ignoreDefaultRules;
        private string _arg_outputFile;
        WriteOnce.ConsoleVerbosity _arg_consoleVerbosityLevel;

        public VerifyRulesCommand(VerifyRulesCommandOptions opt)
        {
            _arg_customRulesPath = opt.CustomRulesPath;
            _arg_ignoreDefaultRules = opt.IgnoreDefaultRules;
            _arg_outputFile = opt.OutputFilePath;
            if (!Enum.TryParse(opt.ConsoleVerbosityLevel, true, out _arg_consoleVerbosityLevel))
                throw new OpException(String.Format(ErrMsg.FormatString(ErrMsg.ID.CMD_INVALID_ARG_VALUE, "-x")));
            WriteOnce.Verbosity = _arg_consoleVerbosityLevel;
            ConfigureOutput();
            ConfigRules();
        }


        private void ConfigureOutput()
        {
            //setup output                       
            TextWriter outputWriter;

            if (!string.IsNullOrEmpty(_arg_outputFile))
            {
                outputWriter = File.CreateText(_arg_outputFile);
                outputWriter.WriteLine(Program.GetVersionString());
                WriteOnce.Writer = outputWriter;
                WriteOnce.Verbosity = WriteOnce.ConsoleVerbosity.Low;
            }
        }


        void ConfigRules()
        {
            _rulePaths = new List<string>();
            if (!_arg_ignoreDefaultRules)
                _rulePaths.Add(Utils.GetPath(Utils.AppPath.defaultRules));

            if (!string.IsNullOrEmpty(_arg_customRulesPath))
                _rulePaths.Add(_arg_customRulesPath);

            if (_rulePaths.Count == 0)
                throw new OpException(ErrMsg.GetString(ErrMsg.ID.CMD_NORULES_SPECIFIED));

            //validation of paths is delayed for Run in this cmd
        }



        public int Run()
        {
            bool issues = false;

            WriteOnce.Operation(ErrMsg.FormatString(ErrMsg.ID.CMD_RUNNING, "Verify Rules"));

            //load [each] rules file separately to report out where a failure is happening 
            RuleSet rules = new RuleSet(WriteOnce.Log);
            IEnumerable<string> fileListing = new List<string>();
            foreach (string rulePath in _rulePaths)
            {
                if (Directory.Exists(rulePath))
                    fileListing = Directory.EnumerateFiles(rulePath, "*.json", SearchOption.AllDirectories);
                else if (File.Exists(rulePath) && Path.GetExtension(rulePath) == ".json")
                    fileListing = new List<string>() { new string(rulePath) };
                else
                {
                    throw new OpException(ErrMsg.FormatString(ErrMsg.ID.CMD_INVALID_RULE_PATH, rulePath));
                }

                //test loading each file
                foreach (string filename in fileListing)
                {
                    try
                    {
                        rules.AddFile(filename);
                        WriteOnce.Info(string.Format("Rule file added {0}", filename), true, WriteOnce.ConsoleVerbosity.High);
                    }
                    catch (Exception e)
                    {
                        WriteOnce.Error(string.Format("Rule file add failed {0}", filename));
                        WriteOnce.SafeLog(e.Message + "\n" + e.StackTrace, NLog.LogLevel.Error);
                        issues = true;
                    }
                }
            }
            
            //option to write validating data
            if (_arg_consoleVerbosityLevel == WriteOnce.ConsoleVerbosity.High)
                WritePartialRuleDetails(rules);

            //final status report
            if (issues)
                WriteOnce.Any(ErrMsg.GetString(ErrMsg.ID.VERIFY_RULES_RESULTS_FAIL), true, ConsoleColor.Red, WriteOnce.ConsoleVerbosity.Low);
            else
                WriteOnce.Any(ErrMsg.GetString(ErrMsg.ID.VERIFY_RULES_RESULTS_SUCCESS), true, ConsoleColor.Green, WriteOnce.ConsoleVerbosity.Low);

            WriteOnce.Operation(ErrMsg.FormatString(ErrMsg.ID.CMD_COMPLETED, "Verify Rules"));
            WriteOnce.FlushAll();
            if (!String.IsNullOrEmpty(_arg_outputFile))
                WriteOnce.Any(ErrMsg.FormatString(ErrMsg.ID.ANALYZE_OUTPUT_FILE, _arg_outputFile), true, ConsoleColor.Gray, WriteOnce.ConsoleVerbosity.Low);

            return issues ? (int)ExitCode.NotVerified : (int)ExitCode.Verified;
        }

        
        void WritePartialRuleDetails(RuleSet rules)
        {
            WriteOnce.Result("RuleId,Rulename,RuleDesc,Tags,AppliesToLanguage", true, WriteOnce.ConsoleVerbosity.High);

            //option to write out partial rule data 
            foreach (Rule r in rules)
            {
                string tags = "";
                string languages = "";
                foreach (string tag in r.Tags)
                    tags += tag + ",";

                tags = tags.Remove(tags.Length - 1);

                if (r.AppliesTo != null && r.AppliesTo.Length > 0)
                {
                    foreach (string lang in r.AppliesTo)
                        languages += lang + ",";

                    languages = languages.Remove(languages.Length - 1);
                }
                else
                    languages = "Not-specified-so-all";

                WriteOnce.Result(string.Format("{0},{1},{2},{3},{4}", r.Id, r.Name, r.Description, tags, languages), true, WriteOnce.ConsoleVerbosity.High);
            }
        }
    }

}
