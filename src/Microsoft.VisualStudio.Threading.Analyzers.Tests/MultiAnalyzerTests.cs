﻿namespace Microsoft.VisualStudio.Threading.Analyzers.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Xunit;
    using Xunit.Abstractions;

    public class MultiAnalyzerTests : DiagnosticVerifier
    {
        public MultiAnalyzerTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        protected override ImmutableArray<DiagnosticAnalyzer> GetCSharpDiagnosticAnalyzers()
        {
            var analyzers = from type in typeof(VSSDK001SynchronousWaitAnalyzer).Assembly.GetTypes()
                            where type.GetCustomAttributes(typeof(DiagnosticAnalyzerAttribute), true).Any()
                            select (DiagnosticAnalyzer)Activator.CreateInstance(type);
            return analyzers.ToImmutableArray();
        }

        [Fact]
        public void JustOneDiagnosticPerLine()
        {
            var test = @"
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

class Test {
    JoinableTaskFactory jtf;

    Task<int> FooAsync() {
        Task t = Task.FromResult(1);
        t.GetAwaiter().GetResult(); // VSSDK001, VSSDK008, VSSDK009
        jtf.Run(async delegate { await BarAsync(); }); // VSSDK008, VSSDK009
        return Task.FromResult(1);
    }

    Task BarAsync() => null;
}";

            this.VerifyNoMoreThanOneDiagnosticPerLine(test);
        }

        /// <summary>
        /// Verifies that no analyzer throws due to a missing interface member.
        /// </summary>
        [Fact]
        public void MissingInterfaceImplementationMember()
        {
            var test = @"
public interface A {
    void Foo();
}

public class Parent : A {
    // This class intentionally does not implement the interface
}

internal class Child : Parent {
    public Child() { }
}
";

            this.VerifyCSharpDiagnostic(new[] { test }, hasEntrypoint: false, allowErrors: true);
        }

        [Fact]
        public void AnonymousTypeObjectCreationSyntax()
        {
            var test = @"
using System;

public class A {
    public void B() {
        var c = new { D = 5 };
    }

    internal void C() {
        var c = new { D = 5 };
    }
}
";

            this.VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void MissingTypeObjectCreationSyntax()
        {
            var test = @"
using System;

public class A {
    public void B() {
        var c = new C();
    }

    internal void C() {
        var c = new C();
    }
}
";

            this.VerifyCSharpDiagnostic(new[] { test }, hasEntrypoint: false, allowErrors: true);
        }

        [Fact]
        public void ManyMethodInvocationStyles()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public class A {
    private Action a;

    public void B() {
        a();
        (a).Invoke();
        D<int>();
        E().ToString();
        E()();
        string v = nameof(E);
    }

    internal void C() {
        a();
        (a).Invoke();
        D<int>();
        E().ToString();
        E()();
        string v = nameof(E);
    }

     public Task BAsync() {
        a();
        (a).Invoke();
        D<int>();
        E().ToString();
        E()();
        string v = nameof(E);
        return null;
    }

    internal Task CAsync() {
        a();
        (a).Invoke();
        D<int>();
        E().ToString();
        E()();
        string v = nameof(E);
        return null;
    }

    private void D<T>() { }

    private Action E() => null;
}
";

            this.VerifyCSharpDiagnostic(test);
        }

        private void VerifyNoMoreThanOneDiagnosticPerLine(string test)
        {
            this.LogFileContent(test);
            ImmutableArray<DiagnosticAnalyzer> analyzers = this.GetCSharpDiagnosticAnalyzers();
            var actualResults = GetSortedDiagnostics(new[] { test }, LanguageNames.CSharp, analyzers, false);
            string diagnosticsOutput = actualResults.Any() ? FormatDiagnostics(analyzers, actualResults.ToArray()) : "    NONE.";
            this.logger.WriteLine("Actual diagnostics:\n" + diagnosticsOutput);

            // Assert that each line only merits at most one diagnostic.
            int lastDiagnosticLine = -1;
            Diagnostic lastDiagnostic = null;
            for (int i = 0; i < actualResults.Length; i++)
            {
                Diagnostic diagnostic = actualResults[i];
                int diagnosticLinePosition = diagnostic.Location.GetLineSpan().StartLinePosition.Line;
                if (lastDiagnosticLine == diagnosticLinePosition)
                {
                    Assert.False(true, $"Both {lastDiagnostic.Id} and {diagnostic.Id} produced diagnostics for line {diagnosticLinePosition + 1}.");
                }

                lastDiagnosticLine = diagnosticLinePosition;
                lastDiagnostic = diagnostic;
            }
        }
    }
}
