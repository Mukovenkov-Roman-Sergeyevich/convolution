# Задача 2

Распараллеливание алгоритма свёртки для одного изображения с использованием различных стратегий декомпозиции данных

## Среда тестирования и эталон

Среда тестирования и конфигурация оборудования полностью идентичны тем, что использовались в Задаче 1.

**Оптимизированная версия (`Sequential Optimized`)**, использующая буферизацию строк, показала себя как наиболее производительный последовательный алгоритм. Её будем использовать как эталон.

## Задача 2: Анализ параллельных реализаций

Были реализованы и протестированы четыре стратегии распараллеливания с использованием Task Parallel Library (TPL), каждая из которых соответствует пунктам технического задания:

1.  **По строкам (`Parallel by Row`):** Изображение делится на ряды, которые обрабатываются параллельно. Низлежащий метод взят из `Sequential Optimized`
2.  **По столбцам (`Parallel by Column`):** Изображение делится на колонки. Низлежащий взят из наивной имплементации ввиду неприменимости быстрого метода.
3.  **По плитке/сетке (`Parallel by Tile`):** Изображение делится на крупные горизонтальные блоки с помощью `Partitioner`. Низлежащий взят из `Sequential Optimized`
4.  **Попиксельно (`Parallel by Pixel`):** Вычисление каждого отдельного пикселя рассматривается как независимая параллельная задача.

### Результаты измерений

Ниже представлена сводная таблица результатов производительности. Ускорение показывает, во сколько раз параллельный метод быстрее, чем эталонный `Sequential Optimized`.

| Метод                    | Ядро    | Размер    | Среднее время | **Ускорение (vs Эталон)**   |
|:-------------------------|:--------|:----------|--------------:|:----------------------------|
| **Sequential Optimized** | Sharpen | 512x512   |      7.01 ms  | **1.00x**                   |
| Parallel by Row          | Sharpen | 512x512   |      1.39 ms  | **5.04x**                   |
| Parallel by Tile         | Sharpen | 512x512   |      1.47 ms  | **4.77x**                   |
| Parallel by Column       | Sharpen | 512x512   |      2.42 ms  | **2.90x**                   |
| Parallel by Pixel        | Sharpen | 512x512   |      2.12 ms  | **3.31x**                   |
| **Sequential Optimized** | Sharpen | 1024x1024 |     26.96 ms  | **1.00x**                   |
| Parallel by Row          | Sharpen | 1024x1024 |      4.27 ms  | **6.31x**                   |
| Parallel by Tile         | Sharpen | 1024x1024 |      4.94 ms  | **5.46x**                   |
| Parallel by Column       | Sharpen | 1024x1024 |      9.05 ms  | **2.98x**                   |
| Parallel by Pixel        | Sharpen | 1024x1024 |      7.54 ms  | **3.58x**                   |
| **Sequential Optimized** | Sharpen | 2048x2048 |    115.91 ms  | **1.00x**                   |
| Parallel by Row          | Sharpen | 2048x2048 |     19.74 ms  | **5.87x**                   |
| Parallel by Tile         | Sharpen | 2048x2048 |     20.68 ms  | **5.60x**                   |
| Parallel by Column       | Sharpen | 2048x2048 |     54.16 ms  | **2.14x**                   |
| Parallel by Pixel        | Sharpen | 2048x2048 |     45.85 ms  | **2.53x**                   |

Для BoxBlur аналогичны и ведут к тем же выводам

---

### Анализ №1: `Parallel by Row` и `Parallel by Tile`

**Гипотеза:** Стратегии, обеспечивающие последовательный доступ к памяти, будут наиболее эффективными.

**Результат:** Данные подтверждают гипотезу. `Parallel by Row` демонстрирует **ускорение до 6.3x**, что является показателем масштабируемости для процессора с 20 логическими ядрами. `Parallel by Tile` показывает почти идентичные результаты.

**Объяснение:** Оба метода делят работу на крупные горизонтальные блоки. Поскольку пиксели в памяти хранятся по строкам (`row-major`), каждый поток читает большие непрерывные участки памяти. Это создаёт идеальные условия для префетчера процессора, который заранее подгружает необходимые данные в сверхбыстрый L1/L2 кэш. Процессорные ядра никогда не простаивают в ожидании данных и максимально утилизируются для вычислений.

---

### Анализ №2: `Parallel by Column`

**Гипотеза:** Распараллеливание по столбцам будет значительно медленнее из-за больших скачков местоположения доступа к кэшу и памяти.

**Результат:** Метод `Parallel by Column` показывает наименьшее ускорение (всего **~2.1x** на большом изображении) и оказывается в **2.7 раза медленнее**, чем `Parallel by Row`.

**Объяснение:** Чтобы обработать один столбец, поток вынужден прыгать по памяти с большим шагом, равным ширине изображения. Это полностью разрушает **пространственную локальность данных**, а также делает оптимизированный метод не применимым. Каждый доступ к новому пикселю в столбце с высокой вероятностью приводит к кэш-промаху, заставляя процессор ждать данные из медленной оперативной памяти.

---

### Анализ №3: `Parallel by Pixel`

**Гипотеза:** Попиксельный подход будет крайне неэффективным из-за огромных накладных расходов на управление задачами.

**Результат:** Метод `Parallel by Pixel` показывает ускорение (**~2.5x**).

**Объяснение:** Этот эксперимент иллюстрирует важность **гранулярности задачи**. Полезная работа для одного пикселя занимает наносекунды, накладные расходы на планирование даже легковесной задачи в TPL измеряются в микросекундах. Система тратит на порядки больше времени на администрирование миллионов микро-задач, чем на сами вычисления. Планировщик TPL спасает этот метод.

---

### Анализ №4: Распределение памяти

Анализ выделяемой памяти.

| Метод (2048x2048)  | Выделено памяти | Причина                                          |
|:-------------------|:----------------|:-------------------------------------------------|
| Sequential         | `~20 KB`        | Минимальные накладные расходы.                   |
| **Parallel by Row/Tile** | **`~36 MB`**    | Создание локального буфера строк для каждого потока/задачи (как оказалось, на ) |
| Parallel by Column | `~9 KB`         | Нет буферизации, минимальные аллокации.         |
| Parallel by Pixel  | `~8 KB`         | Нет буферизации, минимальные аллокации.         |

**Вывод:** Parallel by Row и Tile создают буфер для одной задачи, используют его и потом удаляют. Так происходит для всех параллельных задач, что приводит к такому увеличению выделенной памяти. Если мы будем использовать `var localBuffer = new ThreadLocal<Rgb24[]>(() => new Rgb24[kernelSize * width]);`, то память не будет использоваться, а буффер не будет пересоздаваться. Улучшение в памяти и скорости

---

### Итоговый вывод по Задаче 2

Проведённое исследование демонстрирует, что для достижения ускорения при параллелизации недостаточно просто использовать все ядра процессора. Нужно думать над архитектурой алгоритма и его взаимодействием с аппаратным обеспечением.

1.  **Способ доступа к памяти - первостепенен:** Стратегии, уважающие кэш-иерархию и обеспечивающие последовательный доступ к данным (`Parallel by Row`), на порядки эффективнее тех, что её игнорируют.
2.  **Гранулярность задачи определяет эффективность:** Слишком мелкое дробление работы (`Parallel by Pixel`) приводит к тому, что накладные расходы на управление параллелизмом убирает всю выгоду от него.

# Вывод бенчмарка
```
// * Detailed results *
ParallelConvolutionBenchmark.'Sequential Optimized (Baseline)': DefaultJob [KernelName=BoxBlur, ImageSize=512]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 6.633 ms, StdErr = 0.004 ms (0.06%), N = 15, StdDev = 0.015 ms
Min = 6.613 ms, Q1 = 6.622 ms, Median = 6.629 ms, Q3 = 6.644 ms, Max = 6.664 ms
IQR = 0.022 ms, LowerFence = 6.590 ms, UpperFence = 6.677 ms
ConfidenceInterval = [6.617 ms; 6.650 ms] (CI 99.9%), Margin = 0.016 ms (0.25% of Mean)
Skewness = 0.61, Kurtosis = 1.97, MValue = 2
-------------------- Histogram --------------------
[6.605 ms ; 6.672 ms) | @@@@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Row': DefaultJob [KernelName=BoxBlur, ImageSize=512]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 1.408 ms, StdErr = 0.004 ms (0.32%), N = 12, StdDev = 0.015 ms
Min = 1.371 ms, Q1 = 1.399 ms, Median = 1.411 ms, Q3 = 1.419 ms, Max = 1.425 ms
IQR = 0.020 ms, LowerFence = 1.369 ms, UpperFence = 1.450 ms
ConfidenceInterval = [1.389 ms; 1.428 ms] (CI 99.9%), Margin = 0.020 ms (1.40% of Mean)
Skewness = -0.92, Kurtosis = 3.06, MValue = 2
-------------------- Histogram --------------------
[1.363 ms ; 1.432 ms) | @@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Column': DefaultJob [KernelName=BoxBlur, ImageSize=512]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 2.063 ms, StdErr = 0.010 ms (0.47%), N = 15, StdDev = 0.037 ms
Min = 2.024 ms, Q1 = 2.032 ms, Median = 2.043 ms, Q3 = 2.080 ms, Max = 2.136 ms
IQR = 0.048 ms, LowerFence = 1.961 ms, UpperFence = 2.152 ms
ConfidenceInterval = [2.023 ms; 2.103 ms] (CI 99.9%), Margin = 0.040 ms (1.94% of Mean)
Skewness = 0.67, Kurtosis = 1.97, MValue = 2
-------------------- Histogram --------------------
[2.013 ms ; 2.069 ms) | @@@@@@@@
[2.069 ms ; 2.156 ms) | @@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Tile': DefaultJob [KernelName=BoxBlur, ImageSize=512]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 1.470 ms, StdErr = 0.008 ms (0.56%), N = 37, StdDev = 0.050 ms
Min = 1.264 ms, Q1 = 1.474 ms, Median = 1.484 ms, Q3 = 1.491 ms, Max = 1.505 ms
IQR = 0.017 ms, LowerFence = 1.448 ms, UpperFence = 1.516 ms
ConfidenceInterval = [1.441 ms; 1.500 ms] (CI 99.9%), Margin = 0.029 ms (2.00% of Mean)
Skewness = -3.15, Kurtosis = 12.3, MValue = 2
-------------------- Histogram --------------------
[1.259 ms ; 1.298 ms) | @@
[1.298 ms ; 1.338 ms) | 
[1.338 ms ; 1.378 ms) | 
[1.378 ms ; 1.417 ms) | @
[1.417 ms ; 1.462 ms) | @@
[1.462 ms ; 1.525 ms) | @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Pixel': DefaultJob [KernelName=BoxBlur, ImageSize=512]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 2.270 ms, StdErr = 0.007 ms (0.29%), N = 14, StdDev = 0.025 ms
Min = 2.234 ms, Q1 = 2.252 ms, Median = 2.273 ms, Q3 = 2.282 ms, Max = 2.326 ms
IQR = 0.030 ms, LowerFence = 2.207 ms, UpperFence = 2.326 ms
ConfidenceInterval = [2.242 ms; 2.298 ms] (CI 99.9%), Margin = 0.028 ms (1.24% of Mean)
Skewness = 0.61, Kurtosis = 2.6, MValue = 2
-------------------- Histogram --------------------
[2.221 ms ; 2.285 ms) | @@@@@@@@@@@@
[2.285 ms ; 2.340 ms) | @@
---------------------------------------------------

ParallelConvolutionBenchmark.'Sequential Optimized (Baseline)': DefaultJob [KernelName=BoxBlur, ImageSize=1024]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 28.601 ms, StdErr = 0.029 ms (0.10%), N = 15, StdDev = 0.112 ms
Min = 28.348 ms, Q1 = 28.525 ms, Median = 28.607 ms, Q3 = 28.679 ms, Max = 28.771 ms
IQR = 0.155 ms, LowerFence = 28.293 ms, UpperFence = 28.911 ms
ConfidenceInterval = [28.481 ms; 28.722 ms] (CI 99.9%), Margin = 0.120 ms (0.42% of Mean)
Skewness = -0.46, Kurtosis = 2.52, MValue = 2
-------------------- Histogram --------------------
[28.288 ms ; 28.831 ms) | @@@@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Row': DefaultJob [KernelName=BoxBlur, ImageSize=1024]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 5.513 ms, StdErr = 0.060 ms (1.09%), N = 100, StdDev = 0.604 ms
Min = 4.098 ms, Q1 = 5.164 ms, Median = 5.780 ms, Q3 = 5.883 ms, Max = 6.609 ms
IQR = 0.719 ms, LowerFence = 4.085 ms, UpperFence = 6.961 ms
ConfidenceInterval = [5.308 ms; 5.718 ms] (CI 99.9%), Margin = 0.205 ms (3.71% of Mean)
Skewness = -1.08, Kurtosis = 3.23, MValue = 2.46
-------------------- Histogram --------------------
[4.043 ms ; 4.384 ms) | @@@@@@@@@@@
[4.384 ms ; 4.808 ms) | @@@
[4.808 ms ; 5.263 ms) | @@@@@@@@@@@@@@@
[5.263 ms ; 5.682 ms) | @@@@@@@@@@@
[5.682 ms ; 6.023 ms) | @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
[6.023 ms ; 6.380 ms) | @@@@@@
[6.380 ms ; 6.780 ms) | @@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Column': DefaultJob [KernelName=BoxBlur, ImageSize=1024]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 9.861 ms, StdErr = 0.022 ms (0.22%), N = 15, StdDev = 0.085 ms
Min = 9.646 ms, Q1 = 9.845 ms, Median = 9.888 ms, Q3 = 9.908 ms, Max = 9.967 ms
IQR = 0.063 ms, LowerFence = 9.750 ms, UpperFence = 10.003 ms
ConfidenceInterval = [9.770 ms; 9.952 ms] (CI 99.9%), Margin = 0.091 ms (0.92% of Mean)
Skewness = -1.14, Kurtosis = 3.47, MValue = 2
-------------------- Histogram --------------------
[9.600 ms ; 9.977 ms) | @@@@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Tile': DefaultJob [KernelName=BoxBlur, ImageSize=1024]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 4.961 ms, StdErr = 0.007 ms (0.14%), N = 12, StdDev = 0.024 ms
Min = 4.927 ms, Q1 = 4.946 ms, Median = 4.957 ms, Q3 = 4.975 ms, Max = 5.004 ms
IQR = 0.029 ms, LowerFence = 4.902 ms, UpperFence = 5.019 ms
ConfidenceInterval = [4.931 ms; 4.991 ms] (CI 99.9%), Margin = 0.030 ms (0.61% of Mean)
Skewness = 0.19, Kurtosis = 1.9, MValue = 2
-------------------- Histogram --------------------
[4.914 ms ; 5.015 ms) | @@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Pixel': DefaultJob [KernelName=BoxBlur, ImageSize=1024]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 7.534 ms, StdErr = 0.007 ms (0.09%), N = 14, StdDev = 0.027 ms
Min = 7.475 ms, Q1 = 7.523 ms, Median = 7.533 ms, Q3 = 7.550 ms, Max = 7.572 ms
IQR = 0.027 ms, LowerFence = 7.482 ms, UpperFence = 7.590 ms
ConfidenceInterval = [7.504 ms; 7.564 ms] (CI 99.9%), Margin = 0.030 ms (0.40% of Mean)
Skewness = -0.56, Kurtosis = 2.57, MValue = 2
-------------------- Histogram --------------------
[7.461 ms ; 7.586 ms) | @@@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Sequential Optimized (Baseline)': DefaultJob [KernelName=BoxBlur, ImageSize=2048]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 115.292 ms, StdErr = 0.245 ms (0.21%), N = 24, StdDev = 1.201 ms
Min = 114.269 ms, Q1 = 114.581 ms, Median = 115.041 ms, Q3 = 115.610 ms, Max = 120.333 ms
IQR = 1.029 ms, LowerFence = 113.037 ms, UpperFence = 117.153 ms
ConfidenceInterval = [114.369 ms; 116.216 ms] (CI 99.9%), Margin = 0.924 ms (0.80% of Mean)
Skewness = 3, Kurtosis = 13.02, MValue = 2
-------------------- Histogram --------------------
[113.723 ms ; 117.235 ms) | @@@@@@@@@@@@@@@@@@@@@@@
[117.235 ms ; 120.879 ms) | @
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Row': DefaultJob [KernelName=BoxBlur, ImageSize=2048]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 19.693 ms, StdErr = 0.113 ms (0.57%), N = 76, StdDev = 0.982 ms
Min = 16.680 ms, Q1 = 19.908 ms, Median = 19.989 ms, Q3 = 20.067 ms, Max = 20.316 ms
IQR = 0.158 ms, LowerFence = 19.671 ms, UpperFence = 20.304 ms
ConfidenceInterval = [19.307 ms; 20.079 ms] (CI 99.9%), Margin = 0.386 ms (1.96% of Mean)
Skewness = -2.47, Kurtosis = 7.32, MValue = 2
-------------------- Histogram --------------------
[16.588 ms ; 17.197 ms) | @@@@@@@@
[17.197 ms ; 17.806 ms) | 
[17.806 ms ; 18.415 ms) | 
[18.415 ms ; 19.024 ms) | 
[19.024 ms ; 19.632 ms) | 
[19.632 ms ; 20.375 ms) | @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Column': DefaultJob [KernelName=BoxBlur, ImageSize=2048]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 53.515 ms, StdErr = 0.167 ms (0.31%), N = 13, StdDev = 0.601 ms
Min = 52.869 ms, Q1 = 53.068 ms, Median = 53.282 ms, Q3 = 54.025 ms, Max = 54.667 ms
IQR = 0.957 ms, LowerFence = 51.633 ms, UpperFence = 55.461 ms
ConfidenceInterval = [52.796 ms; 54.235 ms] (CI 99.9%), Margin = 0.719 ms (1.34% of Mean)
Skewness = 0.67, Kurtosis = 1.81, MValue = 2
-------------------- Histogram --------------------
[52.534 ms ; 53.660 ms) | @@@@@@@@@
[53.660 ms ; 54.889 ms) | @@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Tile': DefaultJob [KernelName=BoxBlur, ImageSize=2048]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 20.678 ms, StdErr = 0.049 ms (0.24%), N = 15, StdDev = 0.190 ms
Min = 20.370 ms, Q1 = 20.582 ms, Median = 20.670 ms, Q3 = 20.767 ms, Max = 21.030 ms
IQR = 0.185 ms, LowerFence = 20.303 ms, UpperFence = 21.045 ms
ConfidenceInterval = [20.475 ms; 20.881 ms] (CI 99.9%), Margin = 0.203 ms (0.98% of Mean)
Skewness = 0.4, Kurtosis = 2.28, MValue = 2
-------------------- Histogram --------------------
[20.269 ms ; 21.131 ms) | @@@@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Pixel': DefaultJob [KernelName=BoxBlur, ImageSize=2048]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 46.239 ms, StdErr = 0.223 ms (0.48%), N = 15, StdDev = 0.862 ms
Min = 45.273 ms, Q1 = 45.520 ms, Median = 46.062 ms, Q3 = 47.081 ms, Max = 47.660 ms
IQR = 1.561 ms, LowerFence = 43.179 ms, UpperFence = 49.423 ms
ConfidenceInterval = [45.317 ms; 47.160 ms] (CI 99.9%), Margin = 0.922 ms (1.99% of Mean)
Skewness = 0.44, Kurtosis = 1.49, MValue = 2
-------------------- Histogram --------------------
[45.214 ms ; 47.840 ms) | @@@@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Sequential Optimized (Baseline)': DefaultJob [KernelName=Sharpen, ImageSize=512]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 7.013 ms, StdErr = 0.036 ms (0.52%), N = 32, StdDev = 0.206 ms
Min = 6.643 ms, Q1 = 6.922 ms, Median = 7.113 ms, Q3 = 7.131 ms, Max = 7.220 ms
IQR = 0.209 ms, LowerFence = 6.609 ms, UpperFence = 7.445 ms
ConfidenceInterval = [6.881 ms; 7.145 ms] (CI 99.9%), Margin = 0.132 ms (1.89% of Mean)
Skewness = -1.01, Kurtosis = 2.21, MValue = 2.61
-------------------- Histogram --------------------
[6.638 ms ; 6.876 ms) | @@@@@@@@
[6.876 ms ; 7.052 ms) | @
[7.052 ms ; 7.223 ms) | @@@@@@@@@@@@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Row': DefaultJob [KernelName=Sharpen, ImageSize=512]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 1.392 ms, StdErr = 0.004 ms (0.28%), N = 13, StdDev = 0.014 ms
Min = 1.366 ms, Q1 = 1.384 ms, Median = 1.394 ms, Q3 = 1.396 ms, Max = 1.420 ms
IQR = 0.012 ms, LowerFence = 1.366 ms, UpperFence = 1.415 ms
ConfidenceInterval = [1.375 ms; 1.408 ms] (CI 99.9%), Margin = 0.017 ms (1.19% of Mean)
Skewness = 0.1, Kurtosis = 2.55, MValue = 2
-------------------- Histogram --------------------
[1.359 ms ; 1.428 ms) | @@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Column': DefaultJob [KernelName=Sharpen, ImageSize=512]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 2.416 ms, StdErr = 0.013 ms (0.52%), N = 28, StdDev = 0.067 ms
Min = 2.082 ms, Q1 = 2.420 ms, Median = 2.429 ms, Q3 = 2.434 ms, Max = 2.455 ms
IQR = 0.014 ms, LowerFence = 2.398 ms, UpperFence = 2.455 ms
ConfidenceInterval = [2.370 ms; 2.463 ms] (CI 99.9%), Margin = 0.046 ms (1.92% of Mean)
Skewness = -4.48, Kurtosis = 22.62, MValue = 2
-------------------- Histogram --------------------
[2.053 ms ; 2.111 ms) | @
[2.111 ms ; 2.168 ms) | 
[2.168 ms ; 2.226 ms) | 
[2.226 ms ; 2.284 ms) | 
[2.284 ms ; 2.341 ms) | 
[2.341 ms ; 2.399 ms) | 
[2.399 ms ; 2.457 ms) | @@@@@@@@@@@@@@@@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Tile': DefaultJob [KernelName=Sharpen, ImageSize=512]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 1.466 ms, StdErr = 0.003 ms (0.24%), N = 15, StdDev = 0.013 ms
Min = 1.450 ms, Q1 = 1.455 ms, Median = 1.467 ms, Q3 = 1.474 ms, Max = 1.499 ms
IQR = 0.018 ms, LowerFence = 1.428 ms, UpperFence = 1.501 ms
ConfidenceInterval = [1.452 ms; 1.481 ms] (CI 99.9%), Margin = 0.014 ms (0.98% of Mean)
Skewness = 0.75, Kurtosis = 2.76, MValue = 2
-------------------- Histogram --------------------
[1.443 ms ; 1.506 ms) | @@@@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Pixel': DefaultJob [KernelName=Sharpen, ImageSize=512]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 2.121 ms, StdErr = 0.004 ms (0.20%), N = 13, StdDev = 0.015 ms
Min = 2.100 ms, Q1 = 2.111 ms, Median = 2.118 ms, Q3 = 2.130 ms, Max = 2.152 ms
IQR = 0.020 ms, LowerFence = 2.081 ms, UpperFence = 2.160 ms
ConfidenceInterval = [2.102 ms; 2.139 ms] (CI 99.9%), Margin = 0.018 ms (0.86% of Mean)
Skewness = 0.48, Kurtosis = 2.04, MValue = 2
-------------------- Histogram --------------------
[2.091 ms ; 2.160 ms) | @@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Sequential Optimized (Baseline)': DefaultJob [KernelName=Sharpen, ImageSize=1024]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 26.959 ms, StdErr = 0.015 ms (0.06%), N = 13, StdDev = 0.054 ms
Min = 26.883 ms, Q1 = 26.919 ms, Median = 26.941 ms, Q3 = 27.009 ms, Max = 27.051 ms
IQR = 0.089 ms, LowerFence = 26.786 ms, UpperFence = 27.142 ms
ConfidenceInterval = [26.894 ms; 27.024 ms] (CI 99.9%), Margin = 0.065 ms (0.24% of Mean)
Skewness = 0.44, Kurtosis = 1.67, MValue = 2
-------------------- Histogram --------------------
[26.853 ms ; 27.081 ms) | @@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Row': DefaultJob [KernelName=Sharpen, ImageSize=1024]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 4.268 ms, StdErr = 0.015 ms (0.35%), N = 13, StdDev = 0.054 ms
Min = 4.148 ms, Q1 = 4.254 ms, Median = 4.264 ms, Q3 = 4.306 ms, Max = 4.366 ms
IQR = 0.052 ms, LowerFence = 4.176 ms, UpperFence = 4.383 ms
ConfidenceInterval = [4.203 ms; 4.333 ms] (CI 99.9%), Margin = 0.065 ms (1.52% of Mean)
Skewness = -0.33, Kurtosis = 2.86, MValue = 2
-------------------- Histogram --------------------
[4.118 ms ; 4.238 ms) | @@@
[4.238 ms ; 4.397 ms) | @@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Column': DefaultJob [KernelName=Sharpen, ImageSize=1024]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 9.046 ms, StdErr = 0.040 ms (0.45%), N = 14, StdDev = 0.151 ms
Min = 8.855 ms, Q1 = 8.956 ms, Median = 9.004 ms, Q3 = 9.115 ms, Max = 9.375 ms
IQR = 0.159 ms, LowerFence = 8.717 ms, UpperFence = 9.354 ms
ConfidenceInterval = [8.876 ms; 9.216 ms] (CI 99.9%), Margin = 0.170 ms (1.88% of Mean)
Skewness = 0.92, Kurtosis = 2.68, MValue = 2
-------------------- Histogram --------------------
[8.773 ms ; 9.061 ms) | @@@@@@@@@@
[9.061 ms ; 9.440 ms) | @@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Tile': DefaultJob [KernelName=Sharpen, ImageSize=1024]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 4.936 ms, StdErr = 0.004 ms (0.08%), N = 13, StdDev = 0.014 ms
Min = 4.912 ms, Q1 = 4.927 ms, Median = 4.937 ms, Q3 = 4.944 ms, Max = 4.958 ms
IQR = 0.017 ms, LowerFence = 4.902 ms, UpperFence = 4.969 ms
ConfidenceInterval = [4.919 ms; 4.953 ms] (CI 99.9%), Margin = 0.017 ms (0.35% of Mean)
Skewness = -0.14, Kurtosis = 1.9, MValue = 2
-------------------- Histogram --------------------
[4.904 ms ; 4.966 ms) | @@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Pixel': DefaultJob [KernelName=Sharpen, ImageSize=1024]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 7.537 ms, StdErr = 0.009 ms (0.11%), N = 12, StdDev = 0.030 ms
Min = 7.481 ms, Q1 = 7.526 ms, Median = 7.544 ms, Q3 = 7.560 ms, Max = 7.575 ms
IQR = 0.034 ms, LowerFence = 7.476 ms, UpperFence = 7.610 ms
ConfidenceInterval = [7.499 ms; 7.575 ms] (CI 99.9%), Margin = 0.038 ms (0.50% of Mean)
Skewness = -0.56, Kurtosis = 1.95, MValue = 2
-------------------- Histogram --------------------
[7.464 ms ; 7.592 ms) | @@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Sequential Optimized (Baseline)': DefaultJob [KernelName=Sharpen, ImageSize=2048]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 115.909 ms, StdErr = 0.312 ms (0.27%), N = 19, StdDev = 1.360 ms
Min = 114.702 ms, Q1 = 114.923 ms, Median = 115.337 ms, Q3 = 116.396 ms, Max = 120.450 ms
IQR = 1.473 ms, LowerFence = 112.714 ms, UpperFence = 118.605 ms
ConfidenceInterval = [114.685 ms; 117.133 ms] (CI 99.9%), Margin = 1.224 ms (1.06% of Mean)
Skewness = 1.87, Kurtosis = 6.78, MValue = 2
-------------------- Histogram --------------------
[114.183 ms ; 118.220 ms) | @@@@@@@@@@@@@@@@@@
[118.220 ms ; 121.119 ms) | @
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Row': DefaultJob [KernelName=Sharpen, ImageSize=2048]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 19.744 ms, StdErr = 0.115 ms (0.58%), N = 71, StdDev = 0.965 ms
Min = 16.879 ms, Q1 = 19.969 ms, Median = 20.093 ms, Q3 = 20.179 ms, Max = 20.400 ms
IQR = 0.210 ms, LowerFence = 19.654 ms, UpperFence = 20.495 ms
ConfidenceInterval = [19.351 ms; 20.138 ms] (CI 99.9%), Margin = 0.394 ms (1.99% of Mean)
Skewness = -2.14, Kurtosis = 5.93, MValue = 2
-------------------- Histogram --------------------
[16.791 ms ; 17.403 ms) | @@@@@@@
[17.403 ms ; 18.324 ms) | @@
[18.324 ms ; 19.124 ms) | @
[19.124 ms ; 19.820 ms) | 
[19.820 ms ; 20.432 ms) | @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Column': DefaultJob [KernelName=Sharpen, ImageSize=2048]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 54.155 ms, StdErr = 0.150 ms (0.28%), N = 14, StdDev = 0.562 ms
Min = 53.575 ms, Q1 = 53.680 ms, Median = 53.995 ms, Q3 = 54.608 ms, Max = 55.189 ms
IQR = 0.928 ms, LowerFence = 52.287 ms, UpperFence = 56.001 ms
ConfidenceInterval = [53.521 ms; 54.790 ms] (CI 99.9%), Margin = 0.634 ms (1.17% of Mean)
Skewness = 0.57, Kurtosis = 1.68, MValue = 2
-------------------- Histogram --------------------
[53.269 ms ; 55.495 ms) | @@@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Tile': DefaultJob [KernelName=Sharpen, ImageSize=2048]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 20.680 ms, StdErr = 0.054 ms (0.26%), N = 14, StdDev = 0.201 ms
Min = 20.456 ms, Q1 = 20.530 ms, Median = 20.640 ms, Q3 = 20.792 ms, Max = 21.094 ms
IQR = 0.261 ms, LowerFence = 20.138 ms, UpperFence = 21.184 ms
ConfidenceInterval = [20.454 ms; 20.907 ms] (CI 99.9%), Margin = 0.227 ms (1.10% of Mean)
Skewness = 0.61, Kurtosis = 2.05, MValue = 2
-------------------- Histogram --------------------
[20.346 ms ; 21.096 ms) | @@@@@@@@@@@@@@
---------------------------------------------------

ParallelConvolutionBenchmark.'Parallel by Pixel': DefaultJob [KernelName=Sharpen, ImageSize=2048]
Runtime = .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2; GC = Concurrent Workstation
Mean = 45.851 ms, StdErr = 0.150 ms (0.33%), N = 15, StdDev = 0.579 ms
Min = 45.118 ms, Q1 = 45.355 ms, Median = 45.721 ms, Q3 = 46.192 ms, Max = 47.066 ms
IQR = 0.837 ms, LowerFence = 44.099 ms, UpperFence = 47.448 ms
ConfidenceInterval = [45.232 ms; 46.470 ms] (CI 99.9%), Margin = 0.619 ms (1.35% of Mean)
Skewness = 0.64, Kurtosis = 2.12, MValue = 2
-------------------- Histogram --------------------
[44.972 ms ; 47.374 ms) | @@@@@@@@@@@@@@@
---------------------------------------------------

// * Summary *

BenchmarkDotNet v0.15.1, Linux Ubuntu 24.04.2 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H 4.70GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 9.0.301
  [Host]     : .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX2


| Method                            | KernelName | ImageSize | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|---------------------------------- |----------- |---------- |-----------:|----------:|----------:|-----------:|------:|--------:|----------:|----------:|------------:|------------:|
| 'Sequential Optimized (Baseline)' | BoxBlur    | 512       |   6.633 ms | 0.0164 ms | 0.0153 ms |   6.629 ms |  1.00 |    0.00 |         - |         - |     5.57 KB |        1.00 |
| 'Parallel by Row'                 | BoxBlur    | 512       |   1.408 ms | 0.0197 ms | 0.0154 ms |   1.411 ms |  0.21 |    0.00 |  332.0313 |    5.8594 |  2322.71 KB |      417.27 |
| 'Parallel by Column'              | BoxBlur    | 512       |   2.063 ms | 0.0399 ms | 0.0373 ms |   2.043 ms |  0.31 |    0.01 |         - |         - |     6.77 KB |        1.22 |
| 'Parallel by Tile'                | BoxBlur    | 512       |   1.470 ms | 0.0294 ms | 0.0499 ms |   1.484 ms |  0.22 |    0.01 |  332.0313 |    7.8125 |  2326.25 KB |      417.91 |
| 'Parallel by Pixel'               | BoxBlur    | 512       |   2.270 ms | 0.0282 ms | 0.0250 ms |   2.273 ms |  0.34 |    0.00 |         - |         - |     6.63 KB |        1.19 |
|                                   |            |           |            |           |           |            |       |         |           |           |             |             |
| 'Sequential Optimized (Baseline)' | BoxBlur    | 1024      |  28.601 ms | 0.1202 ms | 0.1124 ms |  28.607 ms |  1.00 |    0.01 |         - |         - |    10.04 KB |        1.00 |
| 'Parallel by Row'                 | BoxBlur    | 1024      |   5.513 ms | 0.2047 ms | 0.6036 ms |   5.780 ms |  0.19 |    0.02 |  765.6250 |   15.6250 |  9246.79 KB |      920.99 |
| 'Parallel by Column'              | BoxBlur    | 1024      |   9.861 ms | 0.0911 ms | 0.0852 ms |   9.888 ms |  0.34 |    0.00 |         - |         - |     6.92 KB |        0.69 |
| 'Parallel by Tile'                | BoxBlur    | 1024      |   4.961 ms | 0.0303 ms | 0.0237 ms |   4.957 ms |  0.17 |    0.00 |  789.0625 |   31.2500 |   9250.4 KB |      921.35 |
| 'Parallel by Pixel'               | BoxBlur    | 1024      |   7.534 ms | 0.0301 ms | 0.0267 ms |   7.533 ms |  0.26 |    0.00 |         - |         - |     6.92 KB |        0.69 |
|                                   |            |           |            |           |           |            |       |         |           |           |             |             |
| 'Sequential Optimized (Baseline)' | BoxBlur    | 2048      | 115.292 ms | 0.9237 ms | 1.2011 ms | 115.041 ms |  1.00 |    0.01 |         - |         - |    19.59 KB |        1.00 |
| 'Parallel by Row'                 | BoxBlur    | 2048      |  19.693 ms | 0.3860 ms | 0.9824 ms |  19.989 ms |  0.17 |    0.01 | 3000.0000 | 1031.2500 | 36920.12 KB |    1,884.28 |
| 'Parallel by Column'              | BoxBlur    | 2048      |  53.515 ms | 0.7192 ms | 0.6006 ms |  53.282 ms |  0.46 |    0.01 |         - |         - |     9.09 KB |        0.46 |
| 'Parallel by Tile'                | BoxBlur    | 2048      |  20.678 ms | 0.2028 ms | 0.1897 ms |  20.670 ms |  0.18 |    0.00 | 3031.2500 |  781.2500 | 36923.72 KB |    1,884.46 |
| 'Parallel by Pixel'               | BoxBlur    | 2048      |  46.239 ms | 0.9215 ms | 0.8620 ms |  46.062 ms |  0.40 |    0.01 |         - |         - |     7.99 KB |        0.41 |
|                                   |            |           |            |           |           |            |       |         |           |           |             |             |
| 'Sequential Optimized (Baseline)' | Sharpen    | 512       |   7.013 ms | 0.1324 ms | 0.2062 ms |   7.113 ms |  1.00 |    0.04 |         - |         - |     5.51 KB |        1.00 |
| 'Parallel by Row'                 | Sharpen    | 512       |   1.392 ms | 0.0166 ms | 0.0138 ms |   1.394 ms |  0.20 |    0.01 |  332.0313 |    5.8594 |  2322.72 KB |      421.56 |
| 'Parallel by Column'              | Sharpen    | 512       |   2.416 ms | 0.0465 ms | 0.0667 ms |   2.429 ms |  0.34 |    0.01 |         - |         - |     6.78 KB |        1.23 |
| 'Parallel by Tile'                | Sharpen    | 512       |   1.466 ms | 0.0144 ms | 0.0134 ms |   1.467 ms |  0.21 |    0.01 |  332.0313 |    5.8594 |  2326.21 KB |      422.20 |
| 'Parallel by Pixel'               | Sharpen    | 512       |   2.121 ms | 0.0183 ms | 0.0153 ms |   2.118 ms |  0.30 |    0.01 |         - |         - |     6.67 KB |        1.21 |
|                                   |            |           |            |           |           |            |       |         |           |           |             |             |
| 'Sequential Optimized (Baseline)' | Sharpen    | 1024      |  26.959 ms | 0.0651 ms | 0.0543 ms |  26.941 ms |  1.00 |    0.00 |         - |         - |    10.04 KB |        1.00 |
| 'Parallel by Row'                 | Sharpen    | 1024      |   4.268 ms | 0.0651 ms | 0.0543 ms |   4.264 ms |  0.16 |    0.00 |  765.6250 |   31.2500 |  9246.88 KB |      921.00 |
| 'Parallel by Column'              | Sharpen    | 1024      |   9.046 ms | 0.1702 ms | 0.1509 ms |   9.004 ms |  0.34 |    0.01 |         - |         - |     7.01 KB |        0.70 |
| 'Parallel by Tile'                | Sharpen    | 1024      |   4.936 ms | 0.0171 ms | 0.0143 ms |   4.937 ms |  0.18 |    0.00 |  789.0625 |   39.0625 |  9250.36 KB |      921.35 |
| 'Parallel by Pixel'               | Sharpen    | 1024      |   7.537 ms | 0.0381 ms | 0.0297 ms |   7.544 ms |  0.28 |    0.00 |         - |         - |     6.93 KB |        0.69 |
|                                   |            |           |            |           |           |            |       |         |           |           |             |             |
| 'Sequential Optimized (Baseline)' | Sharpen    | 2048      | 115.909 ms | 1.2240 ms | 1.3604 ms | 115.337 ms |  1.00 |    0.02 |         - |         - |    19.59 KB |        1.00 |
| 'Parallel by Row'                 | Sharpen    | 2048      |  19.744 ms | 0.3935 ms | 0.9653 ms |  20.093 ms |  0.17 |    0.01 | 3000.0000 | 1062.5000 | 36920.21 KB |    1,884.28 |
| 'Parallel by Column'              | Sharpen    | 2048      |  54.155 ms | 0.6345 ms | 0.5624 ms |  53.995 ms |  0.47 |    0.01 |         - |         - |     8.61 KB |        0.44 |
| 'Parallel by Tile'                | Sharpen    | 2048      |  20.680 ms | 0.2268 ms | 0.2010 ms |  20.640 ms |  0.18 |    0.00 | 3031.2500 |  906.2500 | 36924.01 KB |    1,884.48 |
| 'Parallel by Pixel'               | Sharpen    | 2048      |  45.851 ms | 0.6192 ms | 0.5792 ms |  45.721 ms |  0.40 |    0.01 |         - |         - |     7.65 KB |        0.39 |

// * Hints *
Outliers
  ParallelConvolutionBenchmark.'Parallel by Row': Default                 -> 3 outliers were removed (1.50 ms..1.52 ms)
  ParallelConvolutionBenchmark.'Parallel by Tile': Default                -> 3 outliers were detected (1.26 ms..1.40 ms)
  ParallelConvolutionBenchmark.'Parallel by Pixel': Default               -> 1 outlier  was  removed (2.34 ms)
  ParallelConvolutionBenchmark.'Parallel by Column': Default              -> 2 outliers were detected (9.65 ms, 9.72 ms)
  ParallelConvolutionBenchmark.'Parallel by Tile': Default                -> 3 outliers were removed (5.17 ms..5.73 ms)
  ParallelConvolutionBenchmark.'Parallel by Pixel': Default               -> 1 outlier  was  removed, 2 outliers were detected (7.48 ms, 7.61 ms)
  ParallelConvolutionBenchmark.'Sequential Optimized (Baseline)': Default -> 7 outliers were removed (123.96 ms..130.25 ms)
  ParallelConvolutionBenchmark.'Parallel by Row': Default                 -> 1 outlier  was  removed, 9 outliers were detected (16.68 ms..17.10 ms, 20.52 ms)
  ParallelConvolutionBenchmark.'Parallel by Column': Default              -> 2 outliers were removed (60.05 ms, 66.81 ms)
  ParallelConvolutionBenchmark.'Sequential Optimized (Baseline)': Default -> 1 outlier  was  removed, 8 outliers were detected (6.64 ms..6.69 ms, 7.47 ms)
  ParallelConvolutionBenchmark.'Parallel by Row': Default                 -> 2 outliers were removed (1.47 ms, 1.48 ms)
  ParallelConvolutionBenchmark.'Parallel by Column': Default              -> 2 outliers were removed, 3 outliers were detected (2.08 ms, 2.53 ms, 2.59 ms)
  ParallelConvolutionBenchmark.'Parallel by Pixel': Default               -> 2 outliers were removed (2.20 ms, 2.35 ms)
  ParallelConvolutionBenchmark.'Sequential Optimized (Baseline)': Default -> 2 outliers were removed (29.18 ms, 29.26 ms)
  ParallelConvolutionBenchmark.'Parallel by Row': Default                 -> 2 outliers were removed, 3 outliers were detected (4.15 ms, 4.75 ms, 4.88 ms)
  ParallelConvolutionBenchmark.'Parallel by Column': Default              -> 1 outlier  was  removed (9.55 ms)
  ParallelConvolutionBenchmark.'Parallel by Tile': Default                -> 2 outliers were removed (5.01 ms, 5.07 ms)
  ParallelConvolutionBenchmark.'Parallel by Pixel': Default               -> 3 outliers were removed (7.71 ms..8.15 ms)
  ParallelConvolutionBenchmark.'Sequential Optimized (Baseline)': Default -> 6 outliers were removed (132.64 ms..143.22 ms)
  ParallelConvolutionBenchmark.'Parallel by Row': Default                 -> 8 outliers were removed, 18 outliers were detected (16.88 ms..18.82 ms, 20.62 ms..22.98 ms)
  ParallelConvolutionBenchmark.'Parallel by Column': Default              -> 1 outlier  was  removed (56.59 ms)
  ParallelConvolutionBenchmark.'Parallel by Tile': Default                -> 1 outlier  was  removed (21.95 ms)

// * Legends *
  KernelName  : Value of the 'KernelName' parameter
  ImageSize   : Value of the 'ImageSize' parameter
  Mean        : Arithmetic mean of all measurements
  Error       : Half of 99.9% confidence interval
  StdDev      : Standard deviation of all measurements
  Median      : Value separating the higher half of all measurements (50th percentile)
  Ratio       : Mean of the ratio distribution ([Current]/[Baseline])
  RatioSD     : Standard deviation of the ratio distribution ([Current]/[Baseline])
  Gen0        : GC Generation 0 collects per 1000 operations
  Gen1        : GC Generation 1 collects per 1000 operations
  Allocated   : Allocated memory per single operation (managed only, inclusive, 1KB = 1024B)
  Alloc Ratio : Allocated memory ratio distribution ([Current]/[Baseline])
  1 ms        : 1 Millisecond (0.001 sec)

// * Diagnostic Output - MemoryDiagnoser *


// ***** BenchmarkRunner: End *****
Run time: 00:09:53 (593.33 sec), executed benchmarks: 30

Global total time: 00:10:34 (634.77 sec), executed benchmarks: 30
// * Artifacts cleanup *
Artifacts cleanup is finished