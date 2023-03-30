// See https://aka.ms/new-console-template for more information

using Kaitai;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Reflection;

var palette = Assembly.GetExecutingAssembly().GetManifestResourceStream("PetzFlmExtractor.PALETTE.bmp");
var palettebitmap = new Bitmap(palette);
var files = Directory.GetFiles(".").Where(x => x.EndsWith(".toy") || x.EndsWith(".clo"));
var typearg = "-b";
var extractall = false;
var filefailures = new List<string>();

if (args.Length > 0) {
    if (args.Contains("-p")) {
        typearg = "-p";
    }
    if(args.Contains("-a"))
    {
        extractall = true;
    }
}
foreach (var file in files)
{
    try
    {
        Console.WriteLine(file);
        var asm = new PeNet.PeFile(file);
        var resourceTypes = asm.ImageResourceDirectory.DirectoryEntries.Where(x => x.NameResolved == "FLM" || x.NameResolved == "FLH").SelectMany(x => x.ResourceDirectory.DirectoryEntries);
        var entries = resourceTypes.SelectMany(x => x.ResourceDirectory.DirectoryEntries);
        var flhmap = new Dictionary<string, byte[]>();
        var flmmap = new Dictionary<string, byte[]>();
        var createdimage = false;
        var singlefileanimname = "restinga";
        if(file.EndsWith(".clo"))
        {
            singlefileanimname = "awaya";
        }

        foreach (var entry in entries)
        {
            var type = entry.Parent.Parent.Parent.Parent.NameResolved;
            var name = entry.Parent.Parent.NameResolved;
            var offset = entry.ResourceDataEntry.OffsetToData;
            var size = entry.ResourceDataEntry.Size1;
            var span = asm.RawFile.AsSpan(offset, size).ToArray();

            if (type == "FLH")
            {
                if(extractall || !name.ToLower().Contains("away") || file.EndsWith(".clo"))
                {
                    flhmap.Add(name, span);
                }
            }
            else
            {
                if (extractall || !name.ToLower().Contains("away") || file.EndsWith(".clo"))
                {
                    flmmap.Add(name, span);
                }
            }
        }

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
                if ((framename.ToLower().Contains(singlefileanimname) && (frame.Flags & 2) != 0) || extractall)
                {
                    createdimage = true;
                    var offset = (int)frame.Offset;

                    var bitmap = new Bitmap(frame.X2 - frame.X1, frame.Y2 - frame.Y1, PixelFormat.Format8bppIndexed);
                    int x = bitmap.Width;
                    if (bitmap.Width % 4 != 0)
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

                    if (typearg == "-p")
                    {
                        saveformat = ImageFormat.Png;
                        bitmap.MakeTransparent(palettebitmap.Palette.Entries[253]);
                        ending = ".png";
                    }
                    var filename = Path.GetFileNameWithoutExtension(file) + ending;
                    if(extractall)
                    {
                        filename = Path.GetFileNameWithoutExtension(file) + "-" + name + "-" + framename + animcount.ToString() + "-" + framecount.ToString() + ending;
                    }
                    System.IO.Directory.CreateDirectory(".\\Extracts");
                    bitmap.Save(".\\Extracts\\" + filename, saveformat);
                }

                framecount++;

                if((frame.Flags & 4) != 0)
                {
                    framecount = 0;
                    animcount++;
                }
            }
        }

        if (!createdimage)
        {
            filefailures.Add(file + ": created 0 images");
        }
    } catch(Exception ex)
    {
        Console.WriteLine(file + " failed");
        filefailures.Add(file + ": " + ex.StackTrace);
    }
}

if(filefailures.Count > 0)
{

    Console.WriteLine("Failures:");
    foreach (var f in filefailures)
    {
        Console.WriteLine(f);
    }
}


