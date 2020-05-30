using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using Context = Android.Content.Context;
using Android.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Numerics;
using Android.Content;
using OpenTK;

namespace INS1105
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, ISensorEventListener
    {
        //public string writePath = @"C:\SomeDir\hta.txt";
        //string text = "Привет мир!\nПока мир...";
        double dt; // отрезое между снятием ускорения в 2 точках
        double allt; //все время 
        long lasttime;
        
        protected SensorManager msensorManager;

        static MadgwickAHRS AHRS = new MadgwickAHRS(1f / 256f, 5f);


        private double[] accelDataCalibrate;
        private double[] giroscopeData;
        private double pitch, tilt, azimuth;
        //  private double[] accelDataClbr;

        protected Button start;
        protected Button stop;
        protected Button reset;
        protected Button calibrate;
        protected Button write;

        //   protected ImageView image;

        private TextView _aView;
        private TextView _vView;
        private TextView _rView;
        
        private TextView girox;
        private TextView giroy;
        private TextView giroz;

        public TextView QuaterionFieldX;
        public TextView QuaterionFieldY;
        public TextView QuaterionFieldZ;
        public TextView QuaterionField;

        public TextView Pitch;
        public TextView Tilt;
        public TextView Azimuth;

        public TextView PitchMadj;
        public TextView TiltMadj;
        public TextView AzimuthMadj;



        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);

            msensorManager = (SensorManager)GetSystemService(Context.SensorService);

            _aView = (TextView)FindViewById(Resource.Id.textViewAValue);
            _vView = (TextView)FindViewById(Resource.Id.textViewVValue);
            _rView = (TextView)FindViewById(Resource.Id.textViewRValue);
            
            QuaterionFieldX = (TextView)FindViewById(Resource.Id.textViewValueQuaternionX);
            QuaterionFieldY = (TextView)FindViewById(Resource.Id.textViewValueQuaternionY);
            QuaterionFieldZ = (TextView)FindViewById(Resource.Id.textViewValueQuaternionZ);
            QuaterionField = (TextView)FindViewById(Resource.Id.textViewValueQuaternion);

            Pitch = (TextView)FindViewById(Resource.Id.textViewPitch);
            Tilt = (TextView)FindViewById(Resource.Id.textViewTilt);
            Azimuth = (TextView)FindViewById(Resource.Id.textViewAzimuth);

            PitchMadj = (TextView)FindViewById(Resource.Id.textViewPitchMadj);
            TiltMadj = (TextView)FindViewById(Resource.Id.textViewTiltMadj);
            AzimuthMadj = (TextView)FindViewById(Resource.Id.textViewAzimuthMadj);

            girox = (TextView)FindViewById(Resource.Id.textViewValueGiroscopeX);
            giroy = (TextView)FindViewById(Resource.Id.textViewValueGiroscopeY);
            giroz = (TextView)FindViewById(Resource.Id.textViewValueGiroscopeZ);

            start = FindViewById<Button>(Resource.Id.buttonSet0);
            stop = FindViewById<Button>(Resource.Id.buttonStop);
            reset = FindViewById<Button>(Resource.Id.buttonReset);
            calibrate = FindViewById<Button>(Resource.Id.buttonCalibrate);
            write = FindViewById<Button>(Resource.Id.buttonWrite);

            /*
                < ImageView
                android: id = "id/imageVieww"
                android: layout_width = "wrap_content"
                android: layout_height = "400dp"
                android: src = "@drawable/imageproxy"
                android: layout_marginTop = "0.0dp"
             />
                 image = FindViewById<ImageView>(Resource.Id.imageVieww);
                image.SetImageResource(Resource.Drawable.imageproxy);
            */
            start.Click += delegate (object sender, EventArgs e)
            {
                start.Text = "Running...";
                OnResume();
            };
            stop.Click += delegate (object sender, EventArgs e)
            {
                start.Text = "START";
                OnPause();
            };
            reset.Click += delegate (object sender, EventArgs e)
            {
                // на кнопку recet происходит обнуление накопленных значение по скорости и перемещению
                _dR = Vector3d.Zero;
                _V = Vector3d.Zero;
                allt = 0;

                _vView.Text = $"{_V.X:#00.00}, {_V.Y:#00.00}, {_V.Z:#00.00} m/s";
                _rView.Text = $"{_dR.X:#00.00}, {_dR.Y:#00.00}, {_dR.Z:#00.00} m";
            };

            calibrate.Click += delegate (object sender, EventArgs e)
            {
                _sumA = Vector3d.Zero;
                _clbrA = Vector3d.Zero;
                _averageCounter = 0;
            };

            write.Click += delegate (object sender, EventArgs e)
            {
                write.Text = "Writing...";
                WriteFile();
            };
        }

        override protected void OnResume()
        {
            base.OnResume();
            //msensorManager.RegisterListener(this, msensorManager.GetDefaultSensor(SensorType.LinearAcceleration), SensorDelay.Game);
            msensorManager.RegisterListener(this, msensorManager.GetDefaultSensor(SensorType.Accelerometer), SensorDelay.Game);
            msensorManager.RegisterListener(this, msensorManager.GetDefaultSensor(SensorType.Gyroscope), SensorDelay.Game);
            msensorManager.RegisterListener(this, msensorManager.GetDefaultSensor(SensorType.RotationVector), SensorDelay.Game);
        }
        override protected void OnPause()
        {
            base.OnPause();
            //msensorManager.UnregisterListener(this, msensorManager.GetDefaultSensor(SensorType.LinearAcceleration));
            msensorManager.RegisterListener(this, msensorManager.GetDefaultSensor(SensorType.Accelerometer), SensorDelay.Game);
            msensorManager.UnregisterListener(this, msensorManager.GetDefaultSensor(SensorType.Gyroscope));
            msensorManager.UnregisterListener(this, msensorManager.GetDefaultSensor(SensorType.RotationVector));

        }

        private const int AveargeCount = 50; // Будем калибровать только вручную
        int _averageCounter = AveargeCount+1;

        Vector3d _sumA = Vector3d.Zero;
        Vector3d _clbrA = Vector3d.Zero;

        private Vector3d? _Aclbr;

        private Vector3d _V = Vector3d.Zero;
        private Vector3d _dR = Vector3d.Zero;


        public double[] g = null;
        private void LoadNewSensorData(SensorEvent e)
        {
            var type = e.Sensor.Type; //Определяем тип датчика
            if (type == SensorType.Gyroscope)
            {
                giroscopeData = ToArray(e.Values);
            }

            if (type == SensorType.RotationVector)
            {
                double[] g = ToArray(e.Values);

                double norm = Math.Sqrt(g[0] * g[0] + g[1] * g[1] + g[2] * g[2] + g[3] * g[3]);
                g[0] /= norm;
                g[1] /= norm;
                g[2] /= norm;
                g[3] /= norm;
                //Set values to commonly known quaternion letter representatives
                double x = g[0];
                double y = g[1];
                double z = g[2];
                double w = g[3];
                //Calculate Pitch in degrees (-180 to 180)
                double sinP = 2.0 * (w * x + y * z);
                double cosP = 1.0 - 2.0 * (x * x + y * y);

                pitch = Math.Atan2(sinP, cosP) * (180 / Math.PI);

                //Calculate Tilt in degrees (-90 to 90)
                double sinT = 2.0 * (w * y - z * x);
                if (Math.Abs(sinT) >= 1)
                {
                    tilt = Math.PI / 2 * (180 / Math.PI);  // tilt = Math.Copysign(Math.PI / 2, sinT) * (180 / Math.PI); этот вариант правильный, так было в оригинале
                }
                else
                    tilt = Math.Asin(sinT) * (180 / Math.PI);

                //Calculate Azimuth in degrees (0 to 360; 0 = North, 90 = East, 180 = South, 270 = West)
                double sinA = 2.0 * (w * z + x * y);
                double cosA = 1.0 - 2.0 * (y * y + z * z);
                azimuth = Math.Atan2(sinA, cosA) * (180 / Math.PI);
            }


            //if (type == SensorType.LinearAcceleration) 
            if (type == SensorType.Accelerometer)
            {
                var curA = ToVector3d(e.Values);

                dt = (e.Timestamp - lasttime) * 1e-9;
                lasttime = e.Timestamp; //время между двумя последними событиями(снятиями показаний с датчика)
                allt += dt; //все время от нажатия на сброс

                _sumA += curA;
                _averageCounter++;

                if (_averageCounter == AveargeCount)
                    _clbrA = _sumA / 50;

                _Aclbr = curA - _clbrA;

                _V += _Aclbr.Value * dt; //первое интегрирование, получение скорости
                _dR += _V * dt; //второе интегрирование, получение перемещения по каждой из координат
            }
        }

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        { }
        public void WriteFile()
        {
            String FILENAME = "YULIA";
            /* using (var ios = OpenFileInput(FILENAME))
             { 
             // отрываем поток для записи
             BufferedWriter bw = new BufferedWriter(new OutputStreamWriter(OpenFileRequest(FILENAME)));
             // пишем данные
             bw.Write("dd");
             // закрываем поток
             bw.Close();
             // Log.d(LOG_TAG, "Файл записан");
              }*/

            // string FILENAME = "hello_file";
            //string str = "hello world!";

            /*using (var fos = OpenFileOutput(FILENAME, FileCreationMode.Private))
            {
                //get the byte array
                byte[] bytes = GetBytes(str);
                fos.Write(bytes, 0, bytes.Length);
            }*/
            using (var ios = OpenFileOutput(FILENAME, FileCreationMode.MultiProcess))
            {
                // string strs;
                //  using (OutputStreamWriter sr = new OutputStreamWriter(ios))
                // {
                //   using (BufferedWriter br = new BufferedWriter(sr))
                //  {
                //  StringBuilder sb = new StringBuilder();
                string line = "Yulia";
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(line);
                // ios.Write(line,0,3);
                ios.Write(bytes, 0, bytes.Length);
                ios.Close();

                // }
                // }

            }

        }
        public void OnSensorChanged(SensorEvent e)
        {
            LoadNewSensorData(e);
            if (_Aclbr.HasValue)
            {
                _aView.Text = $"{_Aclbr.Value.X:#00.00}, {_Aclbr.Value.Y:#00.00}, {_Aclbr.Value.Z:#00.00} m/s\u00B2";
                _vView.Text = $"{_V.X:#00.00}, {_V.Y:#00.00}, {_V.Z:#00.00} m/s";
                _rView.Text = $"{_dR.X:#00.00}, {_dR.Y:#00.00}, {_dR.Z:#00.00} m";
            }
            Pitch.Text = pitch.ToString("0.00" + "°");
            Tilt.Text = tilt.ToString("0.00" + "°");
            Azimuth.Text = azimuth.ToString("0.00" + "°");

            if (giroscopeData != null)
            {
                girox.Text = (giroscopeData[0]).ToString("0.000");
                giroy.Text = (giroscopeData[1]).ToString("0.000");
                giroz.Text = (giroscopeData[2]).ToString("0.000");
            }

            if (giroscopeData != null && _Aclbr.HasValue)
            {
                AHRS.Update(deg2rad(giroscopeData[0]), deg2rad(giroscopeData[1]), deg2rad(giroscopeData[2]), _Aclbr.Value);

                QuaterionFieldX.Text = (AHRS.Quaternion[0]).ToString("0.000");
                QuaterionFieldY.Text = (AHRS.Quaternion[1]).ToString("0.000");
                QuaterionFieldZ.Text = (AHRS.Quaternion[2]).ToString("0.000");
                QuaterionField.Text = (AHRS.Quaternion[3]).ToString("0.000");

                if (PitchMadj != null && TiltMadj != null && AzimuthMadj != null)
                {
                    PitchMadj.Text = (AHRS.Angles[2]).ToString("0.00" + "°");
                    TiltMadj.Text = (AHRS.Angles[1]).ToString("0.00" + "°");
                    AzimuthMadj.Text = (AHRS.Angles[0]).ToString("0.00" + "°");
                }

                static double deg2rad(double degrees)
                {
                    return (double)(Math.PI / 180) * degrees;
                }
            }
        }

        double[] ToArray(IEnumerable<float> values)
        {
            return values.Select(val => (double)val).ToArray();
        }

        Vector3d ToVector3d(IList<float> vect)
        {
            return new Vector3d(vect[0], vect[1], vect[2]);
        }

    }
    public class MadgwickAHRS
    {
        // Gets or sets the sample period.
        public double SamplePeriod { get; set; }

        // Gets or sets the algorithm gain beta.
        public double Beta { get; set; }

        /// Gets or sets the Quaternion output.
        // public double[] Quaternion { get; set; } так в оригинате, 07.05
        public double[] Quaternion
        {
            get;
            set;
        }
        public double[] Angles
        {
            get;
            set;
        }

        /// <summary>
        /// Инициализация нового экземпляра класса <see cref="MadgwickAHRS"/> 
        /// </summary>
        /// <param name="samplePeriod">
        /// Период выборки.
        /// </param>
        /// <param name="beta">
        /// Algorithm gain beta.
        /// </param>
        public MadgwickAHRS(double samplePeriod, double beta)
        {
            SamplePeriod = samplePeriod;
            Beta = beta;
            Quaternion = new double[] { 1.0, 0.0, 0.0, 0.0 };
            Angles = new double[3];
        }

        /* void writeFileSD()
         {
             // проверяем доступность SD
             if (Environment.GetExternalStorageState().equals(
                 Environment.MediaUnmounted))
             {
                 Log.Debug(LOG_TAG, "SD-карта не доступна: " + Environment.GetExternalStorageState());
                 return;
             }
             // получаем путь к SD
             File sdPath = Environment.GetExternalStoragePublicDirectory(FILENAME_SD);
             // добавляем свой каталог к пути
             sdPath = new File(sdPath.getAbsolutePath() + "/" + DIR_SD);
             // создаем каталог
             sdPath.mkdirs();
             // формируем объект File, который содержит путь к файлу
             File sdFile = new File(sdPath, FILENAME_SD);
             try
             {
                 // открываем поток для записи
                 BufferedWriter bw = new BufferedWriter(new FileWriter(sdFile));
                 // пишем данные
                 bw.Write("Содержимое файла на SD");
                 // закрываем поток
                 bw.Close();
                 //Log.d(LOG_TAG, "Файл записан на SD: " + sdFile.getAbsolutePath());
             }
             catch (IOException e)
             {
                 e.GetBaseException();
             }
         }*/
        /// Algorithm IMU update method. Requires only gyroscope and accelerometer data.
        /// <param name="gx", <param name="gy",<param name="gz",<param name="ax",<param name="ay",<param name="az",>
        /// Measurement in radians/s.
        /// Optimised for minimal arithmetic. Total ±: 45. Total *: 85. Total /: 3. Total sqrt: 3

        public void Update(double gx, double gy, double gz, Vector3d a)
        {
            double ax = a.X;
            double ay = a.Y;
            double az = a.Z;

            double q1 = Quaternion[0], q2 = Quaternion[1], q3 = Quaternion[2], q4 = Quaternion[3];
            double norm;
            double s1, s2, s3, s4;
            double qDot1, qDot2, qDot3, qDot4;

            // Вспомогательные переменные, чтобы избежать повторной арифметики
            double _2q1 = 2f * q1;
            double _2q2 = 2f * q2;
            double _2q3 = 2f * q3;
            double _2q4 = 2f * q4;
            double _4q1 = 4f * q1;
            double _4q2 = 4f * q2;
            double _4q3 = 4f * q3;
            double _8q2 = 8f * q2;
            double _8q3 = 8f * q3;
            double q1q1 = q1 * q1;
            double q2q2 = q2 * q2;
            double q3q3 = q3 * q3;
            double q4q4 = q4 * q4;

            // Нормализация измерений акселерометра
            norm = (double)Math.Sqrt(ax * ax + ay * ay + az * az);
            if (norm == 0f) return;
            norm = 1.0 / norm;
            ax *= norm;
            ay *= norm;
            az *= norm;

            // Метод градиентного спуска
            s1 = _4q1 * q3q3 + _2q3 * ax + _4q1 * q2q2 - _2q2 * ay;
            s2 = _4q2 * q4q4 - _2q4 * ax + 4f * q1q1 * q2 - _2q1 * ay - _4q2 + _8q2 * q2q2 + _8q2 * q3q3 + _4q2 * az;
            s3 = 4f * q1q1 * q3 + _2q1 * ax + _4q3 * q4q4 - _2q4 * ay - _4q3 + _8q3 * q2q2 + _8q3 * q3q3 + _4q3 * az;
            s4 = 4f * q2q2 * q4 - _2q2 * ax + 4f * q3q3 * q4 - _2q3 * ay;
            norm = 1f / (double)Math.Sqrt(s1 * s1 + s2 * s2 + s3 * s3 + s4 * s4);

            s1 *= norm;
            s2 *= norm;
            s3 *= norm;
            s4 *= norm;

            // Вычисление скорости изменения кватерниона
            qDot1 = 0.5 * (-q2 * gx - q3 * gy - q4 * gz) - Beta * s1;
            qDot2 = 0.5 * (q1 * gx + q3 * gz - q4 * gy) - Beta * s2;
            qDot3 = 0.5 * (q1 * gy - q2 * gz + q4 * gx) - Beta * s3;
            qDot4 = 0.5 * (q1 * gz + q2 * gy - q3 * gx) - Beta * s4;

            //  Интегрирование для получения кватерниона
            q1 += qDot1 * SamplePeriod;
            q2 += qDot2 * SamplePeriod;
            q3 += qDot3 * SamplePeriod;
            q4 += qDot4 * SamplePeriod;

            // Нормализация кватерниона
            norm = 1.0 / (double)Math.Sqrt(q1 * q1 + q2 * q2 + q3 * q3 + q4 * q4);
            Quaternion[0] = q1 * norm;
            Quaternion[1] = q2 * norm;
            Quaternion[2] = q3 * norm;
            Quaternion[3] = q4 * norm;
            /*
            double x = g[0];
            double y = g[1];
            double z = g[2];
            double w = g[3];
            */

            //Set values to commonly known quaternion letter representatives
            double x = Quaternion[0];
            double y = Quaternion[1];
            double z = Quaternion[2];
            double w = Quaternion[3];
            //x w, 
            //  Pitch= Angles[0], Tilt = Angles[1], Azimuth = Angles[2];

            double sinP = 2.0 * (w * x + y * z);
            double cosP = 1.0 - 2.0 * (x * x + y * y);
            double sinT = 2.0 * (w * y - z * x);
            double sinA = 2.0 * (w * z + x * y);
            double cosA = 1.0 - 2.0 * (y * y + z * z);

            Angles[0] = Math.Atan2(sinP, cosP) * (180 / Math.PI);
            if (Math.Abs(sinT) >= 1)
            {
                Angles[1] = Math.PI / 2 * (180 / Math.PI);
            }
            else
                Angles[1] = Math.Asin(sinT) * (180 / Math.PI);

            Angles[2] = Math.Atan2(sinA, cosA) * (180 / Math.PI);

            // string writePath = @"C:\SomeDir\hta.txt";

            // string text = "Привет мир!\nПока мир...";

            // using (StreamWriter sw = new StreamWriter(writePath, false, System.Text.Encoding.Default)
            //{
            //       sw.WriteLine(text);     
            // } 
        }
    }
}

