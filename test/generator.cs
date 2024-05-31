using FluentAssertions;
using FluentAssertions.Execution;
using System.ComponentModel;
using Xunit;
using Bitsquid;

namespace test;

public class GeneratorTest
{
    [Fact]
    public void Empty()
    {
        var gen = new Generator();
        gen.Generate().Should().Be("");
    }

    [Fact]
    public void SingleHeader()
    {
        var gen = new Generator();
        gen.Add("Header", "h1");
        gen.Generate().Should().Be("<h1>Header</h1>\n");
    }

    [Fact]
    public void Sample()
    {
        var gen = new Generator();
        gen.Add("Header", "h1");
        gen.Add("One item", "ul", "li", "p");
        gen.Add(null, "ul");
        gen.Add("Second item", "ul", "li", "p");
        gen.Add("with two lines", "ul", "li", "p");
        gen.Generate().Should().Be(
            "<h1>Header</h1>\n" +
            "\n" +
            "<ul>\n" +
            "\t<li><p>One item</p></li>\n" +
            "\t<li>\n" +
            "\t\t<p>\n" +
            "\t\t\tSecond item\n" +
            "\t\t\twith two lines\n" +
            "\t\t</p>\n" +
            "\t</li>\n" +
            "</ul>\n"
        );
    }

    [Fact]
    public void SampleWith2Null()
    {
        var gen = new Generator();
        gen.Add("Header", "h1");
        gen.Add("One item", "ul", "li", "p");
        gen.Add(null, "ul");
        gen.Add(null, "ul");
        gen.Add("Second item", "ul", "li", "p");
        gen.Add("with two lines", "ul", "li", "p");
        gen.Generate().Should().Be(
            "<h1>Header</h1>\n" +
            "\n" +
            "<ul>\n" +
            "\t<li><p>One item</p></li>\n" +
            "\t<li>\n" +
            "\t\t<p>\n" +
            "\t\t\tSecond item\n" +
            "\t\t\twith two lines\n" +
            "\t\t</p>\n" +
            "\t</li>\n" +
            "</ul>\n"
        );
    }
}