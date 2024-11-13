using System;
using System.Collections.Generic;
using UCNLNav;

namespace uOSM
{
    public static class uOSMTileUtils
    {
        #region Properties

        #endregion

        #region Methods

        public static int Lon2TileX(double lon_deg, int z)
        {
            return (int)(Math.Floor((lon_deg + 180.0) / 360.0 * (1 << z)));
        }

        public static int Lat2TileY(double lon_deg, int z)
        {
            return (int)Math.Floor((1 - Math.Log(Math.Tan(Algorithms.Deg2Rad(lon_deg)) + 1 / Math.Cos(Algorithms.Deg2Rad(lon_deg))) / Math.PI) / 2 * (1 << z));
        }

        public static double TileX2Lon(int x, int z)
        {
            return x / (double)(1 << z) * 360.0 - 180.0;
        }

        public static double TileY2Lat(int y, int z)
        {
            double n = Math.PI - 2.0 * Math.PI * y / (double)(1 << z);
            return Algorithms.Rad2Deg(Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n))));
        }


        public static int ScrollTileCoordinate(int c, int z, int dc)
        {
            int maxTiles = 360 * (1 << z);
            int result = (c + dc) % maxTiles;
            if (result < 0)
                result += maxTiles;
            return result;
        }

        
        public static double TileLongitudalSizeM(double lat_deg, int z)
        {
            return Algorithms.WGS84Ellipsoid.MajorSemiAxis_m * Math.PI * 2 * Math.Cos(Algorithms.Deg2Rad(lat_deg)) / (1 << z);
        }

        public static double TileLongitudalSizeMetersPerPixel(int tileWidthPixels, double lat_deg, int z)
        {
            return TileLongitudalSizeM(lat_deg, z) / tileWidthPixels;
        }

        public static void GetFitTiles(double sWidth, double sHeight, int tWidth, int tHeight,
            double p1_lat_deg, double p1_lon_deg, double p2_lat_deg, double p2_lon_deg,
            int maxZ,
            out double c_lat_deg, out double c_lon_deg,
            out int lu_x, out int lu_y, out int rb_x, out int rb_y, out int z)
        {
            // number of fully visible tiles that we need along horizontal & vertical axis
            int hTiles = Convert.ToInt32(Math.Floor(sWidth / tWidth));
            int vTiles = Convert.ToInt32(Math.Floor(sHeight / tHeight));

            if (hTiles == 0)
                hTiles++;

            if (vTiles == 0)
                vTiles++;

            // at this point we need to estimate at which zoom level
            // the specified rectangle (lu_lon, lu_lat) (rb_lon, rb_lat) can be fitted in
            // hTiles x vTiles

            lu_x = 0;
            lu_y = 0;
            rb_x = 0;
            rb_y = 0;

            // starting from the full zoom out
            z = maxZ;

            bool finished = false;
            while (!finished)
            {
                lu_x = Lon2TileX(p1_lon_deg, z);
                rb_x = Lon2TileX(p2_lon_deg, z);
                lu_y = Lat2TileY(p1_lat_deg, z);
                rb_y = Lat2TileY(p2_lat_deg, z);

                if ((((Math.Abs(lu_x - rb_x) + 1) > hTiles) ||
                    ((Math.Abs(lu_y - rb_y) + 1) > vTiles)) && (z > 0))
                {
                    z--;
                }
                else
                {
                    finished = true;
                }
            }

            int tmp = 0;
            if (lu_x > rb_x)
            {
                tmp = lu_x;
                lu_x = rb_x;
                rb_x = tmp;
            }

            if (lu_y > rb_y)
            {
                tmp = lu_y;
                lu_y = rb_y;
                rb_y = tmp;
            }

            // add tiles if needed
            int actualHTiles = Math.Abs(rb_x - lu_x) + 1;
            int actualVTiles = Math.Abs(rb_y - lu_y) + 1;

            int multiplier = -1;
            while (actualHTiles < hTiles)
            {
                if (multiplier < 0)
                    lu_x = ScrollTileCoordinate(lu_x, z, multiplier);
                else
                    rb_x = ScrollTileCoordinate(rb_x, z, multiplier);

                actualHTiles++;
                multiplier *= -1;
            }

            multiplier = -1;
            while (actualVTiles < vTiles)
            {
                if (multiplier < 0)
                    lu_y = ScrollTileCoordinate(lu_y, z, multiplier);
                else
                    rb_y = ScrollTileCoordinate(rb_y, z, multiplier);

                actualVTiles++;
                multiplier *= -1;
            }

            lu_x = ScrollTileCoordinate(lu_x, z, -1);
            lu_y = ScrollTileCoordinate(lu_y, z, -1);
            rb_x = ScrollTileCoordinate(rb_x, z, 1);
            rb_y = ScrollTileCoordinate(rb_y, z, 1);


            double lu_lon, lu_lat, rb_lon, rb_lat;

            lu_lat = TileY2Lat(ScrollTileCoordinate(lu_y, z, 1), z);
            lu_lon = TileX2Lon(ScrollTileCoordinate(lu_x, z, 1), z);
            rb_lat = TileY2Lat(rb_y, z);            
            rb_lon = TileX2Lon(rb_x, z);
            
            c_lat_deg = (lu_lat + rb_lat) / 2.0;
            c_lon_deg = (lu_lon + rb_lon) / 2.0;
        }

        public static void GetTiles(int viewPortWidth_px, int viewPortHeight_px,
            int twidth_px, int theight_px,
            double desired_center_lat_deg, double desired_center_lon_deg, int zoom,
            out int lu_x, out int lu_y, out int rb_x, out int rb_y,
            out double actual_center_lat_deg, out double actual_center_lon_deg,
            out double x_offset_px, out double y_offset_px)
        {
            int c_tile_x = Lon2TileX(desired_center_lon_deg, zoom);
            int c_tile_y = Lat2TileY(desired_center_lat_deg, zoom);

            lu_x = c_tile_x;
            lu_y = c_tile_y;
            rb_x = c_tile_x;
            rb_y = c_tile_y;

            int tlu, trb;
            bool finish = false;

            while ((Math.Abs(lu_x - rb_x) * twidth_px <= viewPortWidth_px + twidth_px) && !finish)
            {
                tlu = ScrollTileCoordinate(lu_x, zoom, -1);
                trb = ScrollTileCoordinate(rb_x, zoom, 1);
                finish = (tlu == lu_x) || (trb == rb_x);
                lu_x = tlu;
                rb_x = trb;
            }

            finish = false;

            while ((Math.Abs(lu_y - rb_y) * theight_px <= viewPortHeight_px + theight_px) && !finish)
            {
                tlu = ScrollTileCoordinate(lu_y, zoom, -1);
                trb = ScrollTileCoordinate(rb_y, zoom, 1);
                finish = (tlu == lu_y) || (trb == rb_y);
                lu_y = tlu;
                rb_y = trb;
            }

            double ctlat_lu = TileY2Lat(c_tile_y, zoom);
            double ctlon_lu = TileX2Lon(c_tile_x, zoom);
            double ctlat_rb = TileY2Lat(ScrollTileCoordinate(c_tile_y, zoom, 1), zoom);
            double ctlon_rb = TileX2Lon(ScrollTileCoordinate(c_tile_x, zoom, 1), zoom);

            actual_center_lat_deg = (ctlat_lu + ctlat_rb) / 2.0;
            actual_center_lon_deg = (ctlon_lu + ctlon_rb) / 2.0;

            //x_offset_px = twidth_px * (desired_center_lon_deg - actual_center_lon_deg) / (ctlon_lu - ctlon_rb);
            //y_offset_px = theight_px * (desired_center_lat_deg - actual_center_lat_deg) / (ctlat_lu - ctlat_rb);

            x_offset_px = (desired_center_lon_deg - ctlon_lu) * twidth_px / (ctlon_rb - ctlon_lu);
            y_offset_px = (desired_center_lat_deg - ctlat_lu) * theight_px / (ctlat_lu - ctlat_rb);
        }       

        public static List<int[]> GetTilesRecursive(double center_lat_deg, double center_lon_deg, int zoomout, int zoomin)
        {
            int c_tile_x = Lon2TileX(center_lon_deg, zoomout);
            int c_tile_y = Lat2TileY(center_lat_deg, zoomout);            
            
            return GetTilesRecursive(c_tile_x, c_tile_y, zoomout, zoomin);              
        }

        public static List<int[]> GetTilesRecursive(int x, int y, int zoomout, int zoomin)
        {            
            List<int[]> result = new List<int[]>();

            result.Add(new int[] { zoomout, x, y });

            if (zoomout < zoomin)
            {
                int nextz = zoomout + 1;
                result.AddRange(GetTilesRecursive(2 * x, 2 * y, nextz, zoomin));
                result.AddRange(GetTilesRecursive(2 * x + 1, 2 * y, nextz, zoomin));
                result.AddRange(GetTilesRecursive(2 * x, 2 * y + 1, nextz, zoomin));
                result.AddRange(GetTilesRecursive(2 * x + 1, 2 * y + 1, nextz, zoomin));
            }

            return result;
        }

        #endregion
    }
}
