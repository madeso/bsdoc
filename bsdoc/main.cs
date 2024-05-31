// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

var gen = new Bitsquid.Generator();
gen.Add("Header", "h1");
gen.Add("One item", "ul", "li", "p");
gen.Add(null, "ul");
gen.Add("Second item", "ul", "li", "p");
gen.Add("with two lines", "ul", "li", "p");
Console.WriteLine(gen.Generate());

internal static class Extensions
{
    public static IEnumerable<(T value, int i)> each_with_index<T>(this IEnumerable<T> model)
    {
        return model.Select((value, i) => (value, i));
    }

    public static T pop<T>(this List<T> list)
    {
        var last = list.Count - 1;
        var end = list[last];
        list.RemoveAt(last);
        return end;
    }
}