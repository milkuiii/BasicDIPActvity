using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BasicDIPActvity
{
    public partial class Form1 : Form
    {
        Bitmap loaded;

        int cents5 = 0;
        int cents10 = 0;
        int cents25 = 0;
        int peso1 = 0;
        int peso5 = 0;
        int totalCoins = 0;
        double totalPesos = 0;

        public Form1()
        {
            InitializeComponent();
        }

        private int computeOtsuThreshold(Bitmap bmp)
        {
            int[] histogram = new int[256];
            int totalPixels = bmp.Width * bmp.Height;

            // 1. Build grayscale histogram

            for (int c = 0; c < bmp.Width; c++)
            {
                for (int r = 0; r < bmp.Height; r++)
                {
                    Color pixel = bmp.GetPixel(c, r);
                    int gray = (int)(pixel.R + pixel.G + pixel.B) / 3;
                    histogram[gray]++;
                }
            }

            // 2. Compute total sum of all pixel values

            double sumTotal = 0;
            for (int t = 0; t < 256; t++)
            {
                sumTotal += t * histogram[t];
            }

            double sumBackground = 0;
            int weightBackground = 0;
            double maxVariance = 0;
            int bestThreshold = 0;

            // 3. Test all possible thresholds from 0 to 255

            for (int t = 0; t < 256; t++)
            {
                weightBackground += histogram[t];
                if (weightBackground == 0) continue;

                int weightForeground = totalPixels - weightBackground;
                if (weightForeground == 0) break;

                sumBackground += (double)(t * histogram[t]);

                double meanBackground = sumBackground / weightBackground;
                double meanForeground = (sumTotal - sumBackground) / weightForeground;

                // Between-class variance
                double betweenClassVariance = (double)weightBackground * (double)weightForeground * Math.Pow(meanBackground - meanForeground, 2);

                if (betweenClassVariance > maxVariance)
                {
                    maxVariance = betweenClassVariance;
                    bestThreshold = t;
                }
            }

            return bestThreshold;
        }

        private Bitmap applyBinaryFilter()
        {
            // Calculate optimal threshold automatically using Otsu's method
            int threshold = computeOtsuThreshold(loaded);

            Bitmap processed = new Bitmap(loaded.Width, loaded.Height);
            Color pixel;
            int gray;

            for (int c = 0; c < loaded.Width; c++)
                for (int r = 0; r < loaded.Height; r++)
                {
                    pixel = loaded.GetPixel(c, r);
                    gray = (int)(pixel.R + pixel.G + pixel.B) / 3;
                    if (gray < threshold)
                        processed.SetPixel(c, r, Color.Black);
                    else
                        processed.SetPixel(c, r, Color.White);

                }
            return processed;
        }

        private int[,] twoPassCCL(Bitmap processed, Color targetColor)
        {
            // Using the two-pass connected component labeling

            Color temp;
            int w = processed.Width;
            int h = processed.Height;

            int[,] labelMap = new int[w, h];
            int nextLabel = 1;

            int maxLabels = w * h;
            int[] p = new int[maxLabels];
            
            for(int i = 0; i < maxLabels; i++)
            {
                p[i] = i;
            }

            // 1. Assign temp labels 

            for (int r = 0; r < processed.Height; r++)
            {
                for (int c = 0; c < processed.Width; c++)
                {
                    temp = processed.GetPixel(c, r);
                    if (temp.ToArgb() != targetColor.ToArgb()) continue;

                    int left = (c > 0) ? labelMap[c - 1, r] : 0;
                    int top = (r > 0) ? labelMap[c, r - 1] : 0;

                    if (left == 0 && top == 0)
                    {
                        // Reassign label

                        labelMap[c, r] = nextLabel;
                        nextLabel++;
                    }
                    else if (left != 0 && top == 0)
                    {
                        labelMap[c, r] = left;
                    }
                    else if (left == 0 && top != 0)
                    {
                        labelMap[c, r] = top;
                    }
                    else
                    {
                        labelMap[c, r] = Math.Min(left, top);
                        if (left != top)
                            combine(p, left, top);
                    }
                }
            }

            // 2. Resolve equivalent labels

            for(int r = 0; r < processed.Height; r++)
            {
                for(int c = 0; c < processed.Width; c++)
                {
                    if (labelMap[c, r] > 0) 
                        labelMap[c, r] = findParent(p, labelMap[c, r]);
                }
            }

            return labelMap;
        }

        // Coin classification

        private void classifyCoins(List<int> validAreas)
        {
            validAreas.Sort();

            // Estimated cutoff values for different sized coins

            foreach(int area in validAreas)
            {
                if (area < 7900)
                    cents5++;
                else if (area < 10000)
                    cents10++;
                else if (area < 14500)
                    cents25++;
                else if (area < 18500)
                    peso1++;
                else
                    peso5++;
            }
        }

        // Helper functions
        private int findParent(int[] parent, int label)
        {
            if (parent[label] != label) parent[label] = findParent(parent, parent[label]);
            return parent[label];
        }

        private void combine(int[] parent, int label, int label2)
        {
            int r1 = findParent(parent, label);
            int r2 = findParent(parent, label2);

            if(r1 != r2)
            {
                if (r1 < r2)
                {
                    parent[r2] = r1;
                }
                else
                {
                    parent[r1] = r2;
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Ensure values are 0

            cents5 = cents10 = cents25 = peso1 = peso5 = 0;
            totalCoins = 0;
            totalPesos = 0;

            loaded = new Bitmap(pictureBox1.Image);

            Bitmap processed = applyBinaryFilter();
            int [,] labelMap = twoPassCCL(processed, Color.Black);

            // Measure the pixel area per labeled objects

            Dictionary<int, int> coinAreas = new Dictionary<int, int>();
            for (int r = 0; r < processed.Height; r++)
            {
                for (int c = 0; c < processed.Width; c++)
                {
                    int id = labelMap[c, r];
                    if (id > 0)
                    {
                        if (!coinAreas.ContainsKey(id))
                            coinAreas[id] = 0;
                        coinAreas[id]++;
                    }
                }
            }

            // Filtering out noise from the image

            List<int> validAreas = coinAreas.Values
                .Where(a => a >= 500)
                .OrderBy(a => a)
                .ToList();

            classifyCoins(validAreas);

            totalCoins = cents5 + cents10 + cents25 + peso1 + peso5;
            totalPesos = (cents5 * 0.05) + (cents10 * 0.10) + (cents25 * 0.25) + (peso1 * 1.00) + (peso5 * 5.00);

            richTextBox1.Text = "=== Philippine Coin Count Results ===\n\n" +
                $"5 centavos coins: {cents5}\n" +
                $"10 centavos coins: {cents10}\n" +
                $"25 centavos coins: {cents25}\n" +
                $"1 peso coins: {peso1}\n" +
                $"5 peso coins: {peso5}\n\n" +
                $"Total number of coins: {totalCoins}\n" +
                $"Total amount: {totalPesos} Pesos";

        }

    }
}
