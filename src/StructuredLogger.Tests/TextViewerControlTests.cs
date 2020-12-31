using System.Threading.Tasks;
using StructuredLogViewer.Controls;
using VerifyTests;
using Xunit;
using VerifyXunit;

namespace StructuredLogger.Tests
{
    [UsesVerify]
    public class TextViewerControlTests
    {
        static TextViewerControlTests()
        {
            VerifyXaml.Enable();
        }

        [StaFact]
        public Task Render()
        {
            var control = new TextViewerControl();
            control.SetText("theText");
            return Verifier.Verify(control);
        }
    }
}