using HarmonyXLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using NUnit.Framework.Internal;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace HarmonyLibTests.Assets
{
	/*public class HttpWebRequestPatches
	{
		public static bool prefixCalled = false;
		public static bool postfixCalled = false;

		public static void Prefix()
		{
			prefixCalled = true;
		}

		public static void Postfix()
		{
			postfixCalled = true;
		}

		public static void ResetTest()
		{
			prefixCalled = false;
			postfixCalled = false;
		}
	}*/

	public static class EnumeratorPatch
	{
		public static MethodBase patchTarget;
		public static int runTimes = 0;

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(EnumeratorCode), nameof(EnumeratorCode.NumberEnumerator), MethodType.Enumerator)]
		private static IEnumerable<CodeInstruction> MoveNextPatch(IEnumerable<CodeInstruction> instructions, MethodBase original)
		{
			patchTarget = original;
			yield return Transpilers.EmitDelegate<Action>(() => runTimes++);
			foreach (var codeInstruction in instructions)
				yield return codeInstruction;
		}
	}

	[HarmonyPatch]
	public static class PostfixOnExceptionPatch
	{
		public static bool called;
		public static bool patched;

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method5))]
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			patched = true;
			return instructions;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method5))]
		private static void Postfix()
		{
			called = true;
		}
	}

	public static class TypeTargetedPatch
	{
		[HarmonyTranspiler]
		[HarmonyPatch(typeof(DeadEndCode))]
		[HarmonyPatch(nameof(DeadEndCode.Method3), MethodType.Normal)]
		private static IEnumerable<CodeInstruction> ReplaceWithStub(IEnumerable<CodeInstruction> instrs, ILGenerator il)
		{
			return new[] {new CodeInstruction(OpCodes.Ret)};
		}
	}

	[HarmonyPatch(typeof(DeadEndCode))]
	public static class MultiAttributePatchClass1
	{
		public static int callCount = 0;

		[HarmonyPatch(nameof(DeadEndCode.Method))]
		[HarmonyPatch(nameof(DeadEndCode.Method2))]
		[HarmonyPrefix]
		public static bool Prefix(ref string __result)
		{
			callCount++;
			__result = "";
			return false;
		}
	}

	[HarmonyPatch]
	public static class MultiAttributePatchClass2
	{
		public static int callCount = 0;

		[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method))]
		[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method2))]
		[HarmonyPrefix]
		public static bool Prefix(ref string __result)
		{
			callCount++;
			__result = "";
			return false;
		}
	}

	[HarmonyPatch]
	public class TestMultiAttributePatch
	{
		[HarmonyPatch(typeof(MultiAttributePatchCall), nameof(MultiAttributePatchCall.GetValue))]
		[HarmonyPostfix]
		public static void ReplaceGetValue(ref bool __result)
		{
			__result = true;
		}
	}

	public static class SafeWrapPatch
	{
		public static bool called = false;

		[HarmonyPrefix]
		[HarmonyWrapSafe]
		[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method4))]
		public static void Prefix()
		{
			called = true;
			throw new Exception();
		}
	}

	public static class MultiAttributePatch
	{
		public static int callCount = 0;

		[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method))]
		[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method2))]
		[HarmonyPrefix]
		public static bool Prefix(ref string __result)
		{
			callCount++;
			__result = "";
			return false;
		}
	}

	public static class OverloadedCodePatch
	{
		public static int callCount = 0;

		[HarmonyPrefix]
		[HarmonyPatch(typeof(OverloadedCode.Class2), nameof(OverloadedCode.Class2.Method), typeof(string))]
		[HarmonyPatch(typeof(OverloadedCode.Class1), nameof(OverloadedCode.Class1.Method))]
		public static bool Prefix(ref string __result)
		{
			callCount++;
			__result = "";
			return false;
		}
	}

	[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method6))]
	public static class ErrorReportTestPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instrs)
		{
			throw new Exception("Test exception");
		}
	}

	public class OverloadedCode
	{

		public class Class1
		{
			public string Method()
			{
				throw new Exception();
			}
		}

		public class Class2
		{
			public string Method(string str)
			{
				throw new Exception();
			}
		}

	}

	public class EnumeratorCode
	{
		public IEnumerable<int> NumberEnumerator()
		{
			yield return 1;
			yield return 2;
			yield return 3;
			yield return 4;
			yield return 5;
		}
	}

	public class DeadEndCode
	{
		public string Method()
		{
			throw new Exception();
		}

		public string Method2()
		{
			throw new Exception();
		}

		public void Method3()
		{
			throw new Exception();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public int Method4()
		{
			var sum = 0;
			for (var i = 1; i <= 10; i++)
				sum += i;
			return sum;
		}

		public string Method5()
		{
			throw new Exception();
		}

		public string Method6()
		{
			throw new Exception();
		}

		public string Method7()
		{
			throw new Exception();
		}
	}

	public class MultiAttributePatchCall
	{
		public static bool returnValue = false;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public bool GetValue()
		{
			if (returnValue)
				return true;
			return false;
		}
	}

	public static class ModuleLevelCall
	{
		public static Func<int> CreateTestMethod()
		{
			using var ad = AssemblyDefinition.CreateAssembly(
				new AssemblyNameDefinition($"CreateTestMethod{Guid.NewGuid().ToString()}", new Version(1, 0)), "MainModule.dll",
				ModuleKind.Dll);

			var moduleType = ad.MainModule.Types.First(m => m.Name == "<Module>");

			var testMethod = new MethodDefinition("ModuleTest", MethodAttributes.Static | MethodAttributes.Public, ad.MainModule.ImportReference(typeof(int)));
			moduleType.Methods.Add(testMethod);
			var il = testMethod.Body.GetILProcessor();
			il.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, 0);
			il.Emit(Mono.Cecil.Cil.OpCodes.Ret);

			var testType = new TypeDefinition("TestNamespace", "TestType", TypeAttributes.Public, ad.MainModule.ImportReference(typeof(object)));
			ad.MainModule.Types.Add(testType);

			var mainMethod = new MethodDefinition("MainTest", MethodAttributes.Public | MethodAttributes.Static, ad.MainModule.ImportReference(typeof(int)));
			testType.Methods.Add(mainMethod);

			il = mainMethod.Body.GetILProcessor();
			il.Emit(Mono.Cecil.Cil.OpCodes.Call, testMethod);
			il.Emit(Mono.Cecil.Cil.OpCodes.Ret);

			using var ms = new MemoryStream();
			ad.Write(ms);

			var ass = Assembly.Load(ms.ToArray());
			var m = AccessTools.Method(ass.GetType("TestNamespace.TestType"), "MainTest");
			return (Func<int>) Delegate.CreateDelegate(typeof(Func<int>), m);
		}

		public static int Postfix(int result)
		{
			return 1;
		}
	}

	[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method))]
	public class DeadEndCode_Patch1
	{
		static void Prefix()
		{
		}

		static void Postfix()
		{
		}
	}

	[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method7))]
	public class DeadEndCode_Patch2
	{
		public static MethodBase original = null;
		public static Exception exception = null;

		static void Nop() { }

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			for (var i = 1; i <= 10; i++)
				yield return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => Nop()));
			yield return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => Transpiler(null)));
			yield return new CodeInstruction(OpCodes.Ret);
		}

		static void Cleanup(MethodBase original, Exception ex)
		{
			if (original is object)
			{
				DeadEndCode_Patch2.original = original;
				exception = ex;
			}
		}
	}

	[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method))]
	public class DeadEndCode_Patch3
	{
		static void Nop() { }

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			for (var i = 1; i <= 10; i++)
				yield return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => Nop()));
			yield return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => Transpiler(null)));
			yield return new CodeInstruction(OpCodes.Ret);
		}

		static Exception Cleanup(Exception ex)
		{
			return ex is null ? null : new ArgumentException("Test", ex);
		}
	}

	[HarmonyPatch(typeof(DeadEndCode), nameof(DeadEndCode.Method))]
	public class DeadEndCode_Patch4
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			yield return new CodeInstruction(OpCodes.Call, null);
		}

		static Exception Cleanup()
		{
			return null;
		}
	}

	public struct SomeStruct
	{
		public bool accepted;

		public static SomeStruct WasAccepted => new SomeStruct { accepted = true };
		public static SomeStruct WasNotAccepted => new SomeStruct { accepted = false };

		public static implicit operator SomeStruct(bool value)
		{
			return value ? WasAccepted : WasNotAccepted;
		}

		public static implicit operator SomeStruct(string value)
		{
			return new SomeStruct();
		}
	}

	public struct AnotherStruct
	{
		public int x;
		public int y;
		public int z;
	}

	public abstract class AbstractClass
	{
		public virtual SomeStruct Method(string originalDef, AnotherStruct loc)
		{
			return SomeStruct.WasAccepted;
		}
	}

	public class ConcreteClass : AbstractClass
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public override SomeStruct Method(string def, AnotherStruct loc)
		{
			return true;
		}
	}

	[HarmonyPatch(typeof(ConcreteClass))]
	[HarmonyPatch(nameof(ConcreteClass.Method))]
	public static class ConcreteClass_Patch
	{
		static void Prefix(ConcreteClass __instance, string def, AnotherStruct loc)
		{
			TestTools.Log("ConcreteClass_Patch.Method.Prefix");
		}
	}

	[HarmonyPatch(typeof(AppDomain), nameof(AppDomain.GetData))]
	public class ExternalMethod_Patch
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			yield return new CodeInstruction(OpCodes.Ldnull);
			yield return new CodeInstruction(OpCodes.Ret);
		}
	}

	public class EventHandlerTestClass
	{
		public delegate void TestEvent();
		public event TestEvent OnTestEvent;

		public void Run()
		{
			Console.WriteLine("EventHandlerTestClass.Run called");
			OnTestEvent += Handler;
			_ = OnTestEvent.Method;
			Console.WriteLine("EventHandlerTestClass.Run done");
		}

		public void Handler()
		{
			try
			{
				Console.WriteLine("MarshalledTestClass.Handler called");
			}
			catch
			{
				Console.WriteLine("MarshalledTestClass.Handler exception");
			}
		}
	}

	[HarmonyPatch(typeof(EventHandlerTestClass), nameof(EventHandlerTestClass.Handler))]
	public class EventHandlerTestClass_Patch
	{
		static void Prefix()
		{
		}
	}

	public class MarshalledTestClass : MarshalByRefObject
	{
		public void Run()
		{
			Console.WriteLine("MarshalledTestClass.Run called");
			Handler();
			Console.WriteLine("MarshalledTestClass.Run called");
		}

		public void Handler()
		{
			try
			{
				Console.WriteLine("MarshalledTestClass.Handler called");
			}
			catch
			{
				Console.WriteLine("MarshalledTestClass.Handler exception");
			}
		}
	}

	[HarmonyPatch(typeof(MarshalledTestClass), nameof(MarshalledTestClass.Handler))]
	public class MarshalledTestClass_Patch
	{
		static void Prefix()
		{
		}
	}

	public class MarshalledWithEventHandlerTest1Class : MarshalByRefObject
	{
		public delegate void TestEvent();
#pragma warning disable CS0067
		public event TestEvent OnTestEvent;
#pragma warning restore CS0067

		public void Run()
		{
			Console.WriteLine("MarshalledWithEventHandlerTest1Class.Run called");
			OnTestEvent += Handler;
			Console.WriteLine("MarshalledWithEventHandlerTest1Class.Run called");
		}

		public void Handler()
		{
			try
			{
				Console.WriteLine("MarshalledWithEventHandlerTest1Class.Handler called");
			}
			catch
			{
				Console.WriteLine("MarshalledWithEventHandlerTest1Class.Handler exception");
			}
		}
	}

	[HarmonyPatch(typeof(MarshalledWithEventHandlerTest1Class), nameof(MarshalledWithEventHandlerTest1Class.Handler))]
	public class MarshalledWithEventHandlerTest1Class_Patch
	{
		static void Prefix()
		{
		}
	}

	public class MarshalledWithEventHandlerTest2Class : MarshalByRefObject
	{
		public delegate void TestEvent();
		public event TestEvent OnTestEvent;

		public void Run()
		{
			Console.WriteLine("MarshalledWithEventHandlerTest2Class.Run called");
			OnTestEvent += Handler;
			_ = OnTestEvent.Method;
			Console.WriteLine("MarshalledWithEventHandlerTest2Class.Run called");
		}

		public void Handler()
		{
			try
			{
				Console.WriteLine("MarshalledWithEventHandlerTest2Class.Handler called");
			}
			catch
			{
				Console.WriteLine("MarshalledWithEventHandlerTest2Class.Handler exception");
			}
		}
	}

	[HarmonyPatch(typeof(MarshalledWithEventHandlerTest2Class), nameof(MarshalledWithEventHandlerTest2Class.Handler))]
	public class MarshalledWithEventHandlerTest2Class_Patch
	{
		static void Prefix()
		{
		}
	}
}
