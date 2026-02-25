using System;
using System.Reflection;
using Dalamud.Interface.Textures.TextureWraps;

class Program {
    static void Main() {
        foreach (var p in typeof(IDalamudTextureWrap).GetProperties()) {
            Console.WriteLine("Property: " + p.Name + " Type: " + p.PropertyType.Name);
        }
    }
}
