using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Convolution
{
    internal class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll")] private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int mode);
        [DllImport("kernel32.dll")] private static extern bool SetConsoleMode(IntPtr hConsoleHandle, int mode);

        const int STD_OUTPUT_HANDLE = -11;
        const int ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        static void EnableAnsi()
        {
            var handle = GetStdHandle(STD_OUTPUT_HANDLE);
            GetConsoleMode(handle, out var mode);
            SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        }



        static double[,] image;
        static double[,] imageAfter;

        static double[,] sobelX = new double[,]
            {
                { -1, 0, 1 },
                { -2, 0, 2 },
                { -1, 0, 1 }
            };
        static double[,] sobelY = new double[,]
        {
                { -1, -2, -1 },
                {  0,  0,  0 },
                {  1,  2,  1 }
        };
        static double[,] sharpen = new double[,]
        {
                {  0, -1,  0 },
                { -1,  5, -1 },
                {  0, -1,  0 }
        };
        static double[,] gaussian = new double[,]
        {
                { 1, 2, 1 },
                { 2, 4, 2 },
                { 1, 2, 1 }
        };
        static double[,] laplacian = new double[,]
        {
                { 0, -1,  0 },
                { -1, 4, -1 },
                { 0, -1,  0 }
        };

        static void LoadImage(string path)
        {
            Bitmap bitmap = new Bitmap(path);
            image = new double[bitmap.Height, bitmap.Width];

            for (int i = 0; i < bitmap.Height; i++)
            {
                for (int j = 0; j < bitmap.Width; j++)
                {
                    image[i, j] = bitmap.GetPixel(j, i).ToArgb();
                }
            }
        }

        static void SaveImage(string name)
        {
            Bitmap output = new Bitmap(imageAfter.GetLength(1), imageAfter.GetLength(0));

            for (int i = 0; i < imageAfter.GetLength(0); i++)
            {
                for (int j = 0; j < imageAfter.GetLength(1); j++)
                {
                    int fullARGB = Convert.ToInt32(imageAfter[i, j]);
                    (int a, int r, int g, int b) Components = ((fullARGB >> 24 & 0xFF), (fullARGB >> 16 & 0xFF), (fullARGB >> 8 & 0xFF), (fullARGB & 0xFF));

                    output.SetPixel(j, i, Color.FromArgb(Components.a, Components.r, Components.g, Components.b));
                }
            }

            output.Save($"outFile_{name}.jpeg", ImageFormat.Jpeg);
        }

        static void PrintArray(double[,] array)
        {
            for (int i = 0; i < image.GetLength(0); i++)
            {
                for (int j = 0; j < image.GetLength(1); j++)
                {
                    int argb = Convert.ToInt32(array[i, j]);
                    (double a, double r, double g, double b) Components = ((argb >> 24 & 0xFF), (argb >> 16 & 0xFF), (argb >> 8 & 0xFF), (argb & 0xFF));
                    Console.Write($"\x1b[48;2;{Components.r};{Components.g};{Components.b}m");
                    Console.Write(" ");
                    Console.Write("\x1b[0m");
                }
                Console.WriteLine();
            }
        }

        static double[,] Convolve(double[,] kernel)
        {
            double[,] output = new double[image.GetLength(0), image.GetLength(1)];

            for (int i = 0; i < image.GetLength(0); i++)
            {
                for (int j = 0; j < image.GetLength(1); j++)
                {
                    // Generate the matrix around the selected point in image
                    double[,] mat = new double[3, 3];

                    int xCount = 0; int yCount = 0;
                    for (int k = i - 1; k <= i + 1; k++) // height
                    {
                        for (int l = j - 1; l <= j + 1; l++) // width
                        {
                            if (k >= 0 && l >= 0 && k < image.GetLength(0) && l < image.GetLength(1))
                            {
                                mat[xCount, yCount] = image[k, l];
                            }
                            xCount++;
                        }
                        xCount = 0;
                        yCount++;
                    }

                    int result = matMult(mat, kernel);

                    output[i, j] = result;
                }
            }

            return output;
        }

        static int matMult(double[,] Values1, double[,] Values2)
        {
            List<int> components = new List<int> { 0, 0, 0, 0 };

            for (int i = 0; i < Values2.GetLength(0); i++)
            {
                for (int j = 0; j < Values2.GetLength(1); j++)
                {
                    int argb = Convert.ToInt32(Values1[i, j]);
                    (int a, int r, int g, int b) Components = ((argb >> 24 & 0xFF), (argb >> 16 & 0xFF), (argb >> 8 & 0xFF), (argb & 0xFF));
                    List<int> outputs = new List<int> { Components.a, Components.r, Components.g, Components.b, };

                    for (int itr = 0; itr < outputs.Count; itr++)
                    {
                        components[itr] += (int)Math.Round(outputs[itr] * Values2[i, j], 0);
                    }
                }
            }

            for (int c = 0; c < components.Count; c++)
            {
                components[c] = Math.Min(255, Math.Max(0, components[c]));
            }

            int toARGB = (components[0] << 24 | components[1] << 16 | components[2] << 8 | components[3]);

            return toARGB;
        }

        static double[,] matAdd(double[,] Values1, double[,] Values2)
        {
            double[,] output = new double[image.GetLength(0), image.GetLength(1)];

            for (int i = 0; i < Values1.GetLength(0); i++)
            {
                for (int j = 0; j < Values2.GetLength(1); j++)
                {
                    int argb = Convert.ToInt32(Values1[i, j]);
                    int argb2 = Convert.ToInt32(Values2[i, j]);
                    (int a, int r, int g, int b) Components1 = ((argb >> 24 & 0xFF), (argb >> 16 & 0xFF), (argb >> 8 & 0xFF), (argb & 0xFF));
                    (int a, int r, int g, int b) Components2 = ((argb2 >> 24 & 0xFF), (argb2 >> 16 & 0xFF), (argb2 >> 8 & 0xFF), (argb2 & 0xFF));
                    List<int> outputs = new List<int> { Components1.a, Components1.r, Components1.g, Components1.b, };
                    List<int> outputs2 = new List<int> { Components2.a, Components2.r, Components2.g, Components2.b, };

                    List<int> results = new List<int>();
                    for (int item = 0; item < outputs.Count; item++)
                    {
                        results.Add(outputs[item] + outputs2[item]);
                    }

                    output[i, j] = (results[0] << 24 | results[1] << 16 | results[2] << 8 | results[3]);
                }
            }


            return output;
        }

        static void Main(string[] args)
        {
            EnableAnsi();

            LoadImage("mihai-halmi-nistor-12cghPPhSf8-unsplash.jpg");

            double[,] convolved = Convolve(sobelX);
            double[,] convolved2 = Convolve(sobelY);
            double[,] convolved3 = Convolve(laplacian);


            imageAfter = matAdd(convolved, convolved2);
            imageAfter = matAdd(imageAfter, convolved3);
            //PrintArray(imageAfter);


            Console.WriteLine("Enter the name of your image");
            string name = Console.ReadLine();
            SaveImage(name);
            Console.WriteLine("Image saved");

            Console.ReadKey();
        }
    }
}