using HarmonyXLib;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Extras
{
	[TestFixture, NonParallelizable]
	class RetrieveOriginalMethod : TestLogger
	{
		[Test]
		public void Test0()
		{
			var harmony = new Harmony("test-original-method");

			var originalMethod = AccessTools.Method(typeof(RetrieveOriginalMethod), nameof(RetrieveOriginalMethod.PatchTarget));
			var dummyPrefix = AccessTools.Method(typeof(RetrieveOriginalMethod), nameof(RetrieveOriginalMethod.DummyPrefix));

			_ = harmony.Patch(originalMethod, new HarmonyMethod(dummyPrefix));

			PatchTarget();
		}

		private static void ChecksStackTrace()
		{
			var st = new StackTrace(1, false);
			var method = Harmony.GetMethodFromStackframe(st.GetFrame(0));

			// Replacement will be HarmonyLibTests.Extras.RetrieveOriginalMethod.PatchTarget_Patch1
			// We should be able to go from this method, back to HarmonyLibTests.Extras.PatchTarget
			if (method is MethodInfo replacement)
			{
				var original = Harmony.GetOriginalMethod(replacement);
				Assert.NotNull(original);
				Assert.AreEqual(original, AccessTools.Method(typeof(RetrieveOriginalMethod), nameof(RetrieveOriginalMethod.PatchTarget)));
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static void PatchTarget()
		{
			try
			{
				ChecksStackTrace(); // call this from within PatchTarget
				throw new Exception();
			}
			catch
			{
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static void DummyPrefix()
		{
		}
	}
}
