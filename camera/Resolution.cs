using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace camera
{
    class Resolution
    {

        private int width;

        public int Width
        {
            get { return width; }
            set { width = value; }
        }
        private int height;

        public int Height
        {
            get { return height; }
            set { height = value; }
        }

        private int bitCount;

        public int BitCount
        {
            get { return bitCount; }
            set { bitCount = value; }
        }

        public Resolution(int width, int height, int bitCount)
        {
            this.width = width;
            this.height = height;
            this.bitCount = bitCount;
        }
    }
}
