
using System.Drawing;
using System.Globalization;
using System.Text;
namespace uOSM
{
    public class uOSMTile
    {
        #region Properties

        public int Z { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }

        public string Key { get; private set; }

        public Image Tile;

        public bool IsEmpty
        {
            get { return Tile == null; }
        }

        #endregion

        #region Constructor

        public uOSMTile(int z, int x, int y)
            : this(z, x, y, null)
        {
        }

        public uOSMTile(int z, int x, int y, Image image)
        {
            Z = z;
            X = x;
            Y = y;
            Tile = image;
            Key = this.ToString();
        }

        #endregion

        #region Methods

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2}", Z, X, Y);
        }

        public static string GetRelativePathAndFileName(uOSMTile tile)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}\\{1}_{2}.png", tile.Z, tile.X, tile.Y);
        }

        public static string GetRelativePathAndFileName(int zoom, int x, int y)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}\\{1}_{2}.png", zoom, x, y);
        }

        public static string GetRelativeUrl(uOSMTile tile)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}.png", tile.Z, tile.X, tile.Y);
        }

        public static string GetRelativeUrl(int zoom, int x, int y)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}.png", zoom, x, y);
        }

        #endregion
    }
}
