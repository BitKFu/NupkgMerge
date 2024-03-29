﻿// Copyright (c) 2015 Abel Cheng <abelcys@gmail.com>. Licensed under the MIT license.
// Repository: https://nupkgmerge.codeplex.com/

using System;

namespace NuGetPackageMerge
{
	class Program
	{
		static int Main(string[] args)
		{
			CmdArguments cmdArguments = new CmdArguments();

			try
			{
				if (cmdArguments.Parse(args))
				{
					var nupkgMerge = new NupkgMerge(cmdArguments.PrimaryNupkg);
					nupkgMerge.Merge(cmdArguments.SecondNupkg);
					nupkgMerge.Save(cmdArguments.OutputNupkg);
					nupkgMerge.CleanUpLocalFiles();

					Console.WriteLine("Successfully merged '{0}' with '{1}' into '{2}'.",
						cmdArguments.PrimaryNupkg, cmdArguments.SecondNupkg, cmdArguments.OutputNupkg);

					return 0;
				}
				else
					return 1;
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Error.WriteLine(e.Message ?? "Failed!");

				if (e.InnerException != null && e.InnerException.Message != null)
					Console.Error.WriteLine(e.InnerException.Message);

				Console.ResetColor();
				return -1;
			}
		}
	}
}
