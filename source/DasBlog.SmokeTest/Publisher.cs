﻿using System;
using System.Collections.Generic;

namespace DasBlog.SmokeTest
{
	public class Publisher : IPublisher
	{
		public void Publish(IEnumerable<TestResults.Result> results)
		{
			Console.WriteLine("Test Results");
			Console.WriteLine("{0, 30} {1,-6} {2}", "Test", "Passed", "Error");
			Console.WriteLine("{0, 30} {1,-6} {2}", "".PadRight(30, '='), "".PadRight(6, '='), "".PadRight(52, '='));
			foreach (var result in results)
			{
				Console.WriteLine("{0, 30} {1,-6} {2}", result.TestId, result.Success, result.ErrorMessge);
			}
			Console.WriteLine();
		}
		
	}
}
