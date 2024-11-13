using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace uOSM
{

    public class RTArgs : EventArgs
    {
        public int TileNumber { get; private set; }
        public int TilesTotal { get; private set; }

        public RTArgs(int tile, int from)
        {
            TileNumber = tile;
            TilesTotal = from;
        }
    }

    public class TilesReadyEventArgs : EventArgs
    {
        #region Properties

        public List<uOSMTile> Tiles { get; private set; }

        #endregion

        #region Constructor

        public TilesReadyEventArgs(IEnumerable<uOSMTile> _tiles)
        {
            Tiles = new List<uOSMTile>();
            foreach (var tile in _tiles)
            {
                Tiles.Add(tile);
            }
        }

        #endregion
    }

    public class uOSMTileProvider : IDisposable
    {
        #region Properties

        bool disposed = false;

        Dictionary<string, uOSMTile> memTiles;

        public string DB_Path { get; private set; }

        public int MaxMemTiles { get; private set; }

        public Size TileSize { get; private set; }

        public int MaxZoom { get; private set; }

        public bool DownloadingEnabled { get; set; }

        string[] servers;

        Image errImage;
        int svrIdx = 0;

        HttpClient httpClient;

        Random rnd;

        #endregion

        #region Constructor

        public uOSMTileProvider(int maxTiles, int maxZoom, Size tileSize, string database_folder, string[] servers_names) 
        {            
            if ((maxTiles < 0) && (maxTiles > 1024))
                throw new ArgumentOutOfRangeException("maxTiles should be in a range from 1 to 1024");

            rnd = new Random();

            MaxZoom = maxZoom;

            MaxMemTiles = maxTiles;

            TileSize = tileSize;
            errImage = new Bitmap(tileSize.Width, tileSize.Height);

            DB_Path = database_folder;

            servers = new string[servers_names.Length];
            Array.Copy(servers_names, servers, servers_names.Length);

            memTiles = new Dictionary<string, uOSMTile>();

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                   | SecurityProtocolType.Tls11
                   | SecurityProtocolType.Tls12
                   | SecurityProtocolType.Ssl3;

            httpClient = new HttpClient();
            
            var mStringSize = rnd.Next(8, 24);
            var mStringBytes = new byte[mStringSize];
            rnd.NextBytes(mStringBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < mStringSize; i++)
                sb.Append(mStringBytes[i].ToString("X2"));

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(sb.ToString());
        }        

        public uOSMTileProvider(int maxTiles, int maxZoom, Size tileSize, string database_folder, string[] servers_names, string customHNames)
        {
            if ((maxTiles < 0) && (maxTiles > 1024))
                throw new ArgumentOutOfRangeException("maxTiles should be in a range from 1 to 1024");

            rnd = new Random();

            MaxZoom = maxZoom;

            MaxMemTiles = maxTiles;

            TileSize = tileSize;
            errImage = new Bitmap(tileSize.Width, tileSize.Height);

            DB_Path = database_folder;

            servers = new string[servers_names.Length];
            Array.Copy(servers_names, servers, servers_names.Length);

            memTiles = new Dictionary<string, uOSMTile>();

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                   | SecurityProtocolType.Tls11
                   | SecurityProtocolType.Tls12
                   | SecurityProtocolType.Ssl3;

            httpClient = new HttpClient();            
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(customHNames);
        }

        #endregion

        #region Methods

        #region Private

        private string GetNextServer()
        {
            string result = servers[svrIdx];
            svrIdx = (svrIdx + 1) % servers.Length;
            return result;
        }

        private bool IsTileInMemory(uOSMTile tile)
        {
            return memTiles.ContainsKey(tile.Key);
        }

        private void SetTileToMemory(uOSMTile tile)
        {
            if (!memTiles.ContainsKey(tile.Key))
            {
                memTiles.Add(tile.Key, tile);

                if (memTiles.Count > MaxMemTiles)
                {
                    memTiles.Remove(memTiles.Keys.First());
                }
            }
        }

        private bool TryLoadTileImage(uOSMTile tile, out Image tileImage)
        {
            bool result = false;                             
            string tilePath = Path.Combine(DB_Path, uOSMTile.GetRelativePathAndFileName(tile));
            tileImage = null;

            if (File.Exists(tilePath))
            {
                try
                {
                    tileImage = Image.FromFile(tilePath);
                    result = true;
                }
                catch { }
            }

            return result;
        }

        private bool TrySaveTile(uOSMTile tile)
        {
            bool result = false;
            string tilePath = Path.Combine(DB_Path, uOSMTile.GetRelativePathAndFileName(tile));
            string tileDir = Path.GetDirectoryName(tilePath);

            try
            {
                if (!Directory.Exists(tileDir))
                    Directory.CreateDirectory(tileDir);
                tile.Tile.Save(tilePath);
                result = true;
            }
            catch
            {
            }

            return result;
        }

        private bool TryDownloadTileImage(uOSMTile tile, out Image tileImage)
        {
            bool result = false;            
            string url = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", GetNextServer(), uOSMTile.GetRelativeUrl(tile));            

            tileImage = null;
            byte[] bytes = null;
            try
            {
                bytes = httpClient.GetByteArrayAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch { }

            if (bytes != null)
            {
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    tileImage = Image.FromStream(ms);
                    result = true;
                }
            }

            return result;
        }


        private bool IsTileStored(uOSMTile tile)
        {
            string tilePath = Path.Combine(DB_Path, uOSMTile.GetRelativePathAndFileName(tile));
            return File.Exists(tilePath);
        }

        private bool IsTileStored(int zoom, int x, int y)
        {
            string tilePath = Path.Combine(DB_Path, uOSMTile.GetRelativePathAndFileName(zoom, x, y));

            return File.Exists(tilePath);
            
        }

        #endregion

        #region Public

        public async Task<List<uOSMTile>> GetTilesAsync(int z, int fromx, int tox, int fromy, int toy)
        {
            return await Task.Run(() =>
                {
                    return GetTiles(z, fromx, tox, fromy, toy);
                });
        }

        public List<uOSMTile> GetTiles(int z, int fromx, int tox, int fromy, int toy)
        {
            List<uOSMTile> result = new List<uOSMTile>();
            
            for (int x = fromx; x <= tox; x++)
            {
                for (int y = fromy; y <= toy; y++)
                {
                    uOSMTile tile = new uOSMTile(z, x, y);

                    if (IsTileInMemory(tile))
                    {
                        tile.Tile = new Bitmap(memTiles[tile.Key].Tile);
                    }
                    else
                    {
                        if (TryLoadTileImage(tile, out tile.Tile))
                        {
                            SetTileToMemory(tile);
                        }
                        else if (DownloadingEnabled)
                        {
                            Thread.Sleep(rnd.Next(500));
                            if (TryDownloadTileImage(tile, out tile.Tile))
                            {
                                SetTileToMemory(tile);
                                TrySaveTile(tile);
                            }
                            else
                            {
                                tile.Tile = (Bitmap)errImage.Clone();
                            }
                        }
                        else
                        {
                            tile.Tile = (Bitmap)errImage.Clone();
                        }
                    }

                    result.Add(tile);
                }
            }

            return result;
        }



        public bool AbortSaveTilesRecursive = false;

        public void SaveTilesRecursive(double lat_deg, double lon_deg, int zoomout, int zoomin, int delay_ms, bool randomize)
        {
            var tileIDs = uOSMTileUtils.GetTilesRecursive(lat_deg, lon_deg, zoomout, zoomin);

            if (randomize)
            {
                Random rnd = new Random();
                for (int i = tileIDs.Count - 1; i > 0; i--)
                {
                    int sIndex = rnd.Next(i + 1);
                    var tmp = tileIDs[i];
                    tileIDs[i] = tileIDs[sIndex];
                    tileIDs[sIndex] = tmp;
                }
            }

            for (int i = 0; (i < tileIDs.Count) && (!AbortSaveTilesRecursive); i++)
            {
                uOSMTile tile = new uOSMTile(tileIDs[i][0], tileIDs[i][1], tileIDs[i][2]);

                if (!IsTileStored(tile))
                {
                    if (TryDownloadTileImage(tile, out tile.Tile))
                    {
                        TrySaveTile(tile);
                        Thread.Sleep(delay_ms);
                    }
                }

                var tHandler = GotTileEventHandler;
                if (tHandler != null)
                    tHandler(this, new RTArgs(i + 1, tileIDs.Count));
            }
        }

        #endregion

        #endregion

        #region Events

        public EventHandler<TilesReadyEventArgs> TilesReadyEventHandler;

        public EventHandler<RTArgs> GotTileEventHandler;

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    httpClient.Dispose();
                }

                disposed = true;
            }
        }

        #endregion
    }
}
