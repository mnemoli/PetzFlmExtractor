// See https://aka.ms/new-console-template for more information

using Kaitai;
using PeNet.Header.Resource;
using PetzFlmExtractor.Properties;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Resources;
using System.Drawing.Imaging;

var files = Directory.GetFiles(".").Where(x => x.EndsWith(".toy"));
var typearg = "-b";
if (args.Length > 0) {
    typearg = args[0];
}
foreach (var file in files)
{
    Console.WriteLine(file);
    var asm = new PeNet.PeFile(file);
    var resourceTypes = asm.ImageResourceDirectory.DirectoryEntries.Where(x => x.NameResolved == "FLM" || x.NameResolved == "FLH").SelectMany(x => x.ResourceDirectory.DirectoryEntries);
    var entries = resourceTypes.SelectMany(x => x.ResourceDirectory.DirectoryEntries);
    var flhmap = new Dictionary<string, byte[]>();
    var flmmap = new Dictionary<string, byte[]>();

    foreach (var entry in entries)
    { 
        try
        {
            var type = entry.Parent.Parent.Parent.Parent.NameResolved;
            var name = entry.Parent.Parent.NameResolved;
            var offset = entry.ResourceDataEntry.OffsetToData;
            var size = entry.ResourceDataEntry.Size1;
            var span = asm.RawFile.AsSpan(offset, size).ToArray();

            if (type == "FLH")
            {
                flhmap.Add(name, span);
            }
            else
            {
                flmmap.Add(name, span);
            }
        } catch {
            Console.WriteLine("Went wrong");
        }
        
    }

    var palettebitmap = PetzFlmExtractor.Properties.Resources.PALETTE;

    foreach (var flh in flhmap)
    {
        var name = flh.Key;
        var data = flh.Value;
        var flm = flmmap[name];

        var kaitaiflh = new Flh(new KaitaiStream(data));
        int framecount = 0;
        var animcount = 0;
        string framename = "";
        foreach (var frame in kaitaiflh.Frames)
        {
            if((frame.Flags & 2) != 0)
            {
                if (frame.Name != null && frame.Name.Length > 1)
                {
                    framename = frame.Name.Trim('\0');
                }
                else
                {
                    framename = "";
                }

            }
            var offset = (int)frame.Offset;

            var bitmap = new Bitmap(frame.X2 - frame.X1, frame.Y2 - frame.Y1, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            int x = bitmap.Width;
            if(bitmap.Width % 4 != 0)
            {
                x = bitmap.Width + 4 - (bitmap.Width % 4);
            }
            int size = x * bitmap.Height;
            var bmp = flm[offset..(offset + size)];
            var arrayhandle = GCHandle.Alloc(bmp, GCHandleType.Pinned);
            var thelock = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            bitmap.Palette = palettebitmap.Palette;
            Marshal.Copy(bmp, 0, thelock.Scan0, bmp.Length);
            bitmap.UnlockBits(thelock);
            bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);

            var saveformat = ImageFormat.Bmp;
            var ending = ".bmp";

            if(typearg == "-p")
            {
                saveformat = ImageFormat.Png;
                bitmap.MakeTransparent(palettebitmap.Palette.Entries[253]);
                ending = ".png";
            }
            System.IO.Directory.CreateDirectory(".\\Extracts");
            bitmap.Save(".\\Extracts\\" + name + "-" + framename + animcount.ToString() + "-" + framecount.ToString() + ending, saveformat);

            framecount++;

            if((frame.Flags & 4) != 0)
            {
                framecount = 0;
                animcount++;
            }
        }
    }
}


