using System;
using System.Collections.Generic;
using System.Text;

namespace Sushi.Testing;

[SetUpFixture]
public class GlobalSetUp
{
    [OneTimeSetUp]
    public void RunBeforeAnyTests() => FluentAssertions.License.Accepted = true;
}
