using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.Intentions.Util;
using JetBrains.ReSharper.Feature.Services.Util;
using JetBrains.ReSharper.Features.Navigation.Utils;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.ReSharper.UnitTestFramework.Resources;
using JetBrains.TextControl;
using JetBrains.Util;

namespace LuminaReadHelper
{
	[ContextAction(Name="GenerateRead", Description = "Generate static struct Read method", Group="C#", Disabled=false, Priority = 1)]
	class GenerateReadContextAction : ContextActionBase
	{
		public override string Text => "Generate static Read method";
		private readonly IStructDeclaration _currentStruct;

		private readonly Dictionary<string, string> typeDict = new Dictionary<string, string>() {
			{"char", "br.ReadChar();"},
			{"char[]", "br.ReadReadChars(  );"},
			{"byte", "br.ReadByte();"},
			{"sbyte", "br.ReadSByte();"},
			{"byte[]", "br.ReadBytes(  );"},
			{"sbyte[]", "br.ReadBytes(  );"},
			{"short", "br.ReadInt16();"},
			{"ushort", "br.ReadUInt16();"},
			{"short[]", "br.ReadStructures<Int16>(  );"},
			{"ushort[]", "br.ReadStructures<UInt16>();"},
			{"int", "br.ReadInt32();"},
			{"uint", "br.ReadUInt32();"},
			{"int[]", "br.ReadStructures<Int32>(  );"},
			{"uint[]", "br.ReadStructures<UInt32>(  );"},
			{"long", "br.ReadInt64();"},
			{"ulong", "br.ReadUInt64();"},
			{"long[]", "br.ReadStructures<Int64>(  );"},
			{"ulong[]", "br.ReadStructures<UInt64>(  );"}
		};

		public GenerateReadContextAction(LanguageIndependentContextActionDataProvider dataProvider)
		{
			_currentStruct = dataProvider.GetSelectedElement<IStructDeclaration>();
		}

		protected override Action<ITextControl> ExecutePsiTransaction(ISolution solution, IProgressIndicator progress) {
			string type = _currentStruct.DeclaredName;
			ICSharpStatement anchor = null; // I wish there was documentation on this haha :)

			var factory = CSharpElementFactory.GetInstance(_currentStruct);

			var method = factory.CreateTypeMemberDeclaration("public static " + type + " Read( BinaryReader br ) {}") as IMethodDeclaration;

			var st = factory.CreateStatement("long start = br.BaseStream.Position;" + Environment.NewLine);
			method.Body.AddStatementAfter(st, anchor);

			st = factory.CreateStatement($"{type} ret = new {type}();");
			method.Body.AddStatementAfter(st, anchor);

			IFieldDeclaration lastMember = null;
			foreach (var member in _currentStruct.FieldDeclarationsEnumerable) {
				string typeName = member.Type.GetPresentableName(member.Language);

				if (typeDict.TryGetValue(typeName, out string readStmt)) {
					st = factory.CreateStatement($"ret.{member.DeclaredName} = {readStmt}");
					method.Body.AddStatementAfter(st, anchor);
				} else {
					// sorry lumina
					// string call = member.Type.IsArray() ? $"br.ReadStructures<{typeName}>(  );" : $"br.ReadStructure<{typeName}>();";
					st = factory.CreateStatement($"ret.{member.DeclaredName} = {typeName}.Read( br );");
					method.Body.AddStatementAfter(st, anchor);
				}
				lastMember = member;
			}

			st = factory.CreateStatement("return ret;");
			method.Body.AddStatementAfter(st, anchor);

			using (WriteLockCookie.Create()) {
				ModificationUtil.AddChild(_currentStruct, method);
			}

			if (lastMember != null)
				return control => control.Caret.MoveTo(lastMember.GetDocumentStartOffset().Offset, CaretVisualPlacement.DontScrollIfVisible);
			return control => control.Caret.MoveTo(method.GetDocumentStartOffset().Offset, CaretVisualPlacement.DontScrollIfVisible);
		}

		public override bool IsAvailable(IUserDataHolder cache) {
			return _currentStruct != null;
		}
    }
}
