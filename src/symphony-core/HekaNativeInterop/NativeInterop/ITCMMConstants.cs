
namespace Heka.NativeInterop
{
    public partial class ITCMM
    {
        /// MAX_DEVICE_TYPE_NUMBER -> 6
        public const int MAX_DEVICE_TYPE_NUMBER = 6;

        /// ITC16_ID -> 0
        public const int ITC16_ID = 0;

        /// ITC16_MAX_DEVICE_NUMBER -> 16
        public const int ITC16_MAX_DEVICE_NUMBER = 16;

        /// ITC18_ID -> 1
        public const int ITC18_ID = 1;

        /// ITC18_MAX_DEVICE_NUMBER -> 16
        public const int ITC18_MAX_DEVICE_NUMBER = 16;

        /// ITC1600_ID -> 2
        public const int ITC1600_ID = 2;

        /// ITC1600_MAX_DEVICE_NUMBER -> 16
        public const int ITC1600_MAX_DEVICE_NUMBER = 16;

        /// ITC00_ID -> 3
        public const int ITC00_ID = 3;

        /// ITC00_MAX_DEVICE_NUMBER -> 16
        public const int ITC00_MAX_DEVICE_NUMBER = 16;

        /// USB16_ID -> 4
        public const int USB16_ID = 4;

        /// USB16_MAX_DEVICE_NUMBER -> 16
        public const int USB16_MAX_DEVICE_NUMBER = 16;

        /// USB18_ID -> 5
        public const int USB18_ID = 5;

        /// USB18_MAX_DEVICE_NUMBER -> 16
        public const int USB18_MAX_DEVICE_NUMBER = 16;

        /// ITC_MAX_DEVICE_NUMBER -> 16
        public const int ITC_MAX_DEVICE_NUMBER = 16;

        /// ITC_ALL_DEVICES_ID -> -1
        public const int ITC_ALL_DEVICES_ID = -1;

        /// NORMAL_MODE -> 0
        public const int NORMAL_MODE = 0;

        /// SMART_MODE -> 1
        public const int SMART_MODE = 1;

        /// NUMBER_OF_CHANNEL_GROUPS -> 6
        public const int NUMBER_OF_CHANNEL_GROUPS = 6;

        /// MAX_NUMBER_OF_CHANNELS_IN_GROUP -> 32
        public const int MAX_NUMBER_OF_CHANNELS_IN_GROUP = 32;

        /// D2H -> 0x00
        public const int D2H = 0;

        /// H2D -> 0x01
        public const int H2D = 1;

        /// INPUT_GROUP -> D2H
        public const int INPUT_GROUP = ITCMM.D2H;

        /// OUTPUT_GROUP -> H2D
        public const int OUTPUT_GROUP = ITCMM.H2D;

        /// DIGITAL_INPUT -> 0x02
        public const int DIGITAL_INPUT = 2;

        /// DIGITAL_OUTPUT -> 0x03
        public const int DIGITAL_OUTPUT = 3;

        /// AUX_INPUT -> 0x04
        public const int AUX_INPUT = 4;

        /// AUX_OUTPUT -> 0x05
        public const int AUX_OUTPUT = 5;

        /// TEMP_INPUT -> 0x06
        public const int TEMP_INPUT = 6;

        /// NUMBER_OF_D2H_CHANNELS -> 32
        public const int NUMBER_OF_D2H_CHANNELS = 32;

        /// NUMBER_OF_H2D_CHANNELS -> 16
        public const int NUMBER_OF_H2D_CHANNELS = 16;

        /// ITC18_SOFTWARE_SEQUENCE_SIZE -> 4096
        public const int ITC18_SOFTWARE_SEQUENCE_SIZE = 4096;

        /// ITC16_SOFTWARE_SEQUENCE_SIZE -> 256
        public const int ITC16_SOFTWARE_SEQUENCE_SIZE = 256;

        /// ITC1600_SOFTWARE_SEQUENCE_SIZE -> 16
        public const int ITC1600_SOFTWARE_SEQUENCE_SIZE = 16;

        /// ITC00_SOFTWARE_SEQUENCE_SIZE -> 16
        public const int ITC00_SOFTWARE_SEQUENCE_SIZE = 16;

        /// ITC18_NUMBEROFCHANNELS -> 16
        public const int ITC18_NUMBEROFCHANNELS = 16;

        /// ITC18_NUMBEROFOUTPUTS -> 7
        public const int ITC18_NUMBEROFOUTPUTS = 7;

        /// ITC18_NUMBEROFINPUTS -> 9
        public const int ITC18_NUMBEROFINPUTS = 9;

        /// USB18_NUMBEROFCHANNELS -> 18
        public const int USB18_NUMBEROFCHANNELS = 18;

        /// USB18_NUMBEROFOUTPUTS -> 8
        public const int USB18_NUMBEROFOUTPUTS = 8;

        /// USB18_NUMBEROFINPUTS -> 10
        public const int USB18_NUMBEROFINPUTS = 10;

        /// ITC18_NUMBEROFADCINPUTS -> 8
        public const int ITC18_NUMBEROFADCINPUTS = 8;

        /// ITC18_NUMBEROFDACOUTPUTS -> 4
        public const int ITC18_NUMBEROFDACOUTPUTS = 4;

        /// ITC18_NUMBEROFDIGINPUTS -> 1
        public const int ITC18_NUMBEROFDIGINPUTS = 1;

        /// ITC18_NUMBEROFDIGOUTPUTS -> 2
        public const int ITC18_NUMBEROFDIGOUTPUTS = 2;

        /// ITC18_NUMBEROFAUXINPUTS -> 0
        public const int ITC18_NUMBEROFAUXINPUTS = 0;

        /// ITC18_NUMBEROFAUXOUTPUTS -> 1
        public const int ITC18_NUMBEROFAUXOUTPUTS = 1;

        /// USB18_NUMBEROFAUXINPUTS -> 1
        public const int USB18_NUMBEROFAUXINPUTS = 1;

        /// USB18_NUMBEROFAUXOUTPUTS -> 2
        public const int USB18_NUMBEROFAUXOUTPUTS = 2;

        /// ITC18_DA_CH_MASK -> 0x3
        public const int ITC18_DA_CH_MASK = 3;

        /// ITC18_DO0_CH -> 0x4
        public const int ITC18_DO0_CH = 4;

        /// ITC18_DO1_CH -> 0x5
        public const int ITC18_DO1_CH = 5;

        /// ITC18_AUX_CH -> 0x6
        public const int ITC18_AUX_CH = 6;

        /// USB18_AUX_CH_IN -> 0x9
        public const int USB18_AUX_CH_IN = 9;

        /// USB18_AUX_CH_OUT -> 0x7
        public const int USB18_AUX_CH_OUT = 7;

        /// ITC16_NUMBEROFCHANNELS -> 14
        public const int ITC16_NUMBEROFCHANNELS = 14;

        /// ITC16_NUMBEROFOUTPUTS -> 5
        public const int ITC16_NUMBEROFOUTPUTS = 5;

        /// ITC16_NUMBEROFINPUTS -> 9
        public const int ITC16_NUMBEROFINPUTS = 9;

        /// ITC16_DO_CH -> 4
        public const int ITC16_DO_CH = 4;

        /// USB16_NUMBEROFCHANNELS -> 16
        public const int USB16_NUMBEROFCHANNELS = 16;

        /// USB16_NUMBEROFOUTPUTS -> 6
        public const int USB16_NUMBEROFOUTPUTS = 6;

        /// USB16_NUMBEROFINPUTS -> 10
        public const int USB16_NUMBEROFINPUTS = 10;

        /// ITC16_NUMBEROFADCINPUTS -> 8
        public const int ITC16_NUMBEROFADCINPUTS = 8;

        /// ITC16_NUMBEROFDACOUTPUTS -> 4
        public const int ITC16_NUMBEROFDACOUTPUTS = 4;

        /// ITC16_NUMBEROFDIGINPUTS -> 1
        public const int ITC16_NUMBEROFDIGINPUTS = 1;

        /// ITC16_NUMBEROFDIGOUTPUTS -> 1
        public const int ITC16_NUMBEROFDIGOUTPUTS = 1;

        /// ITC16_NUMBEROFAUXINPUTS -> 0
        public const int ITC16_NUMBEROFAUXINPUTS = 0;

        /// ITC16_NUMBEROFAUXOUTPUTS -> 0
        public const int ITC16_NUMBEROFAUXOUTPUTS = 0;

        /// USB16_NUMBEROFAUXINPUTS -> 1
        public const int USB16_NUMBEROFAUXINPUTS = 1;

        /// USB16_NUMBEROFAUXOUTPUTS -> 1
        public const int USB16_NUMBEROFAUXOUTPUTS = 1;

        /// USB16_AUX_CH_IN -> 0x9
        public const int USB16_AUX_CH_IN = 9;

        /// USB16_AUX_CH_OUT -> 0x5
        public const int USB16_AUX_CH_OUT = 5;

        /// ITC1600_NUMBEROFCHANNELS -> 47
        public const int ITC1600_NUMBEROFCHANNELS = 47;

        /// ITC1600_NUMBEROFINPUTS -> 32
        public const int ITC1600_NUMBEROFINPUTS = 32;

        /// ITC1600_NUMBEROFOUTPUTS -> 15
        public const int ITC1600_NUMBEROFOUTPUTS = 15;

        /// ITC1600_NUMBEROFADCINPUTS -> 16
        public const int ITC1600_NUMBEROFADCINPUTS = 16;

        /// ITC1600_NUMBEROFDACOUTPUTS -> 8
        public const int ITC1600_NUMBEROFDACOUTPUTS = 8;

        /// ITC1600_NUMBEROFDIGINPUTS -> 6
        public const int ITC1600_NUMBEROFDIGINPUTS = 6;

        /// ITC1600_NUMBEROFDIGOUTPUTS -> 6
        public const int ITC1600_NUMBEROFDIGOUTPUTS = 6;

        /// ITC1600_NUMBEROFAUXINPUTS -> 8
        public const int ITC1600_NUMBEROFAUXINPUTS = 8;

        /// ITC1600_NUMBEROFAUXOUTPUTS -> 1
        public const int ITC1600_NUMBEROFAUXOUTPUTS = 1;

        /// ITC1600_NUMBEROFTEMPINPUTS -> 2
        public const int ITC1600_NUMBEROFTEMPINPUTS = 2;

        /// ITC1600_NUMBEROFINPUTGROUPS -> 11
        public const int ITC1600_NUMBEROFINPUTGROUPS = 11;

        /// ITC1600_NUMBEROFOUTPUTGROUPS -> 5
        public const int ITC1600_NUMBEROFOUTPUTGROUPS = 5;

        /// ITC00_NUMBEROFCHANNELS -> 48
        public const int ITC00_NUMBEROFCHANNELS = 48;

        /// ITC00_NUMBEROFINPUTS -> 32
        public const int ITC00_NUMBEROFINPUTS = 32;

        /// ITC00_NUMBEROFOUTPUTS -> 16
        public const int ITC00_NUMBEROFOUTPUTS = 16;

        /// ITC00_NUMBEROFADCINPUTS -> 16
        public const int ITC00_NUMBEROFADCINPUTS = 16;

        /// ITC00_NUMBEROFDACOUTPUTS -> 8
        public const int ITC00_NUMBEROFDACOUTPUTS = 8;

        /// ITC00_NUMBEROFDIGINPUTS -> 6
        public const int ITC00_NUMBEROFDIGINPUTS = 6;

        /// ITC00_NUMBEROFDIGOUTPUTS -> 6
        public const int ITC00_NUMBEROFDIGOUTPUTS = 6;

        /// ITC00_NUMBEROFAUXINPUTS -> 8
        public const int ITC00_NUMBEROFAUXINPUTS = 8;

        /// ITC00_NUMBEROFAUXOUTPUTS -> 2
        public const int ITC00_NUMBEROFAUXOUTPUTS = 2;

        /// ITC00_NUMBEROFTEMPINPUTS -> 2
        public const int ITC00_NUMBEROFTEMPINPUTS = 2;

        /// ITC00_NUMBEROFINPUTGROUPS -> 11
        public const int ITC00_NUMBEROFINPUTGROUPS = 11;

        /// ITC00_NUMBEROFOUTPUTGROUPS -> 5
        public const int ITC00_NUMBEROFOUTPUTGROUPS = 5;

        /// ITC16_DA0 -> 0
        public const int ITC16_DA0 = 0;

        /// ITC16_DA1 -> 1
        public const int ITC16_DA1 = 1;

        /// ITC16_DA2 -> 2
        public const int ITC16_DA2 = 2;

        /// ITC16_DA3 -> 3
        public const int ITC16_DA3 = 3;

        /// ITC16_DO -> 4
        public const int ITC16_DO = 4;

        /// ITC16_AD0 -> 0
        public const int ITC16_AD0 = 0;

        /// ITC16_AD1 -> 1
        public const int ITC16_AD1 = 1;

        /// ITC16_AD2 -> 2
        public const int ITC16_AD2 = 2;

        /// ITC16_AD3 -> 3
        public const int ITC16_AD3 = 3;

        /// ITC16_AD4 -> 4
        public const int ITC16_AD4 = 4;

        /// ITC16_AD5 -> 5
        public const int ITC16_AD5 = 5;

        /// ITC16_AD6 -> 6
        public const int ITC16_AD6 = 6;

        /// ITC16_AD7 -> 7
        public const int ITC16_AD7 = 7;

        /// ITC16_DI -> 8
        public const int ITC16_DI = 8;

        /// ITC18_DA0 -> 0
        public const int ITC18_DA0 = 0;

        /// ITC18_DA1 -> 1
        public const int ITC18_DA1 = 1;

        /// ITC18_DA2 -> 2
        public const int ITC18_DA2 = 2;

        /// ITC18_DA3 -> 3
        public const int ITC18_DA3 = 3;

        /// ITC18_DO0 -> 4
        public const int ITC18_DO0 = 4;

        /// ITC18_DO1 -> 5
        public const int ITC18_DO1 = 5;

        /// ITC18_AUX -> 6
        public const int ITC18_AUX = 6;

        /// ITC18_AD0 -> 0
        public const int ITC18_AD0 = 0;

        /// ITC18_AD1 -> 1
        public const int ITC18_AD1 = 1;

        /// ITC18_AD2 -> 2
        public const int ITC18_AD2 = 2;

        /// ITC18_AD3 -> 3
        public const int ITC18_AD3 = 3;

        /// ITC18_AD4 -> 4
        public const int ITC18_AD4 = 4;

        /// ITC18_AD5 -> 5
        public const int ITC18_AD5 = 5;

        /// ITC18_AD6 -> 6
        public const int ITC18_AD6 = 6;

        /// ITC18_AD7 -> 7
        public const int ITC18_AD7 = 7;

        /// ITC18_DI -> 8
        public const int ITC18_DI = 8;

        /// ITC1600_DA0 -> 0
        public const int ITC1600_DA0 = 0;

        /// ITC1600_DA1 -> 1
        public const int ITC1600_DA1 = 1;

        /// ITC1600_DA2 -> 2
        public const int ITC1600_DA2 = 2;

        /// ITC1600_DA3 -> 3
        public const int ITC1600_DA3 = 3;

        /// ITC1600_DA4 -> 4
        public const int ITC1600_DA4 = 4;

        /// ITC1600_DA5 -> 5
        public const int ITC1600_DA5 = 5;

        /// ITC1600_DA6 -> 6
        public const int ITC1600_DA6 = 6;

        /// ITC1600_DA7 -> 7
        public const int ITC1600_DA7 = 7;

        /// ITC1600_DOF0 -> 8
        public const int ITC1600_DOF0 = 8;

        /// ITC1600_DOS00 -> 9
        public const int ITC1600_DOS00 = 9;

        /// ITC1600_DOS01 -> 10
        public const int ITC1600_DOS01 = 10;

        /// ITC1600_DOF1 -> 11
        public const int ITC1600_DOF1 = 11;

        /// ITC1600_DOS10 -> 12
        public const int ITC1600_DOS10 = 12;

        /// ITC1600_DOS11 -> 13
        public const int ITC1600_DOS11 = 13;

        /// ITC1600_HOST -> 14
        public const int ITC1600_HOST = 14;

        /// ITC1600_AD0 -> 0
        public const int ITC1600_AD0 = 0;

        /// ITC1600_AD1 -> 1
        public const int ITC1600_AD1 = 1;

        /// ITC1600_AD2 -> 2
        public const int ITC1600_AD2 = 2;

        /// ITC1600_AD3 -> 3
        public const int ITC1600_AD3 = 3;

        /// ITC1600_AD4 -> 4
        public const int ITC1600_AD4 = 4;

        /// ITC1600_AD5 -> 5
        public const int ITC1600_AD5 = 5;

        /// ITC1600_AD6 -> 6
        public const int ITC1600_AD6 = 6;

        /// ITC1600_AD7 -> 7
        public const int ITC1600_AD7 = 7;

        /// ITC1600_AD8 -> 8
        public const int ITC1600_AD8 = 8;

        /// ITC1600_AD9 -> 9
        public const int ITC1600_AD9 = 9;

        /// ITC1600_AD10 -> 10
        public const int ITC1600_AD10 = 10;

        /// ITC1600_AD11 -> 11
        public const int ITC1600_AD11 = 11;

        /// ITC1600_AD12 -> 12
        public const int ITC1600_AD12 = 12;

        /// ITC1600_AD13 -> 13
        public const int ITC1600_AD13 = 13;

        /// ITC1600_AD14 -> 14
        public const int ITC1600_AD14 = 14;

        /// ITC1600_AD15 -> 15
        public const int ITC1600_AD15 = 15;

        /// ITC1600_SAD0 -> 16
        public const int ITC1600_SAD0 = 16;

        /// ITC1600_SAD1 -> 17
        public const int ITC1600_SAD1 = 17;

        /// ITC1600_SAD2 -> 18
        public const int ITC1600_SAD2 = 18;

        /// ITC1600_SAD3 -> 19
        public const int ITC1600_SAD3 = 19;

        /// ITC1600_SAD4 -> 20
        public const int ITC1600_SAD4 = 20;

        /// ITC1600_SAD5 -> 21
        public const int ITC1600_SAD5 = 21;

        /// ITC1600_SAD6 -> 22
        public const int ITC1600_SAD6 = 22;

        /// ITC1600_SAD7 -> 23
        public const int ITC1600_SAD7 = 23;

        /// ITC1600_TEM0 -> 24
        public const int ITC1600_TEM0 = 24;

        /// ITC1600_TEM1 -> 25
        public const int ITC1600_TEM1 = 25;

        /// ITC1600_DIF0 -> 26
        public const int ITC1600_DIF0 = 26;

        /// ITC1600_DIS00 -> 27
        public const int ITC1600_DIS00 = 27;

        /// ITC1600_DIS01 -> 28
        public const int ITC1600_DIS01 = 28;

        /// ITC1600_DIF1 -> 29
        public const int ITC1600_DIF1 = 29;

        /// ITC1600_DIS10 -> 30
        public const int ITC1600_DIS10 = 30;

        /// ITC1600_DIS11 -> 31
        public const int ITC1600_DIS11 = 31;

        /// ITC00_DA0 -> 0
        public const int ITC00_DA0 = 0;

        /// ITC00_DA1 -> 1
        public const int ITC00_DA1 = 1;

        /// ITC00_DA2 -> 2
        public const int ITC00_DA2 = 2;

        /// ITC00_DA3 -> 3
        public const int ITC00_DA3 = 3;

        /// ITC00_DA4 -> 4
        public const int ITC00_DA4 = 4;

        /// ITC00_DA5 -> 5
        public const int ITC00_DA5 = 5;

        /// ITC00_DA6 -> 6
        public const int ITC00_DA6 = 6;

        /// ITC00_DA7 -> 7
        public const int ITC00_DA7 = 7;

        /// ITC00_DOF0 -> 8
        public const int ITC00_DOF0 = 8;

        /// ITC00_DOS00 -> 9
        public const int ITC00_DOS00 = 9;

        /// ITC00_DOS01 -> 10
        public const int ITC00_DOS01 = 10;

        /// ITC00_DOF1 -> 11
        public const int ITC00_DOF1 = 11;

        /// ITC00_DOS10 -> 12
        public const int ITC00_DOS10 = 12;

        /// ITC00_DOS11 -> 13
        public const int ITC00_DOS11 = 13;

        /// ITC00_HOST0 -> 14
        public const int ITC00_HOST0 = 14;

        /// ITC00_HOST1 -> 15
        public const int ITC00_HOST1 = 15;

        /// ITC00_AD0 -> 0
        public const int ITC00_AD0 = 0;

        /// ITC00_AD1 -> 1
        public const int ITC00_AD1 = 1;

        /// ITC00_AD2 -> 2
        public const int ITC00_AD2 = 2;

        /// ITC00_AD3 -> 3
        public const int ITC00_AD3 = 3;

        /// ITC00_AD4 -> 4
        public const int ITC00_AD4 = 4;

        /// ITC00_AD5 -> 5
        public const int ITC00_AD5 = 5;

        /// ITC00_AD6 -> 6
        public const int ITC00_AD6 = 6;

        /// ITC00_AD7 -> 7
        public const int ITC00_AD7 = 7;

        /// ITC00_AD8 -> 8
        public const int ITC00_AD8 = 8;

        /// ITC00_AD9 -> 9
        public const int ITC00_AD9 = 9;

        /// ITC00_AD10 -> 10
        public const int ITC00_AD10 = 10;

        /// ITC00_AD11 -> 11
        public const int ITC00_AD11 = 11;

        /// ITC00_AD12 -> 12
        public const int ITC00_AD12 = 12;

        /// ITC00_AD13 -> 13
        public const int ITC00_AD13 = 13;

        /// ITC00_AD14 -> 14
        public const int ITC00_AD14 = 14;

        /// ITC00_AD15 -> 15
        public const int ITC00_AD15 = 15;

        /// ITC00_SAD0 -> 16
        public const int ITC00_SAD0 = 16;

        /// ITC00_SAD1 -> 17
        public const int ITC00_SAD1 = 17;

        /// ITC00_SAD2 -> 18
        public const int ITC00_SAD2 = 18;

        /// ITC00_SAD3 -> 19
        public const int ITC00_SAD3 = 19;

        /// ITC00_SAD4 -> 20
        public const int ITC00_SAD4 = 20;

        /// ITC00_SAD5 -> 21
        public const int ITC00_SAD5 = 21;

        /// ITC00_SAD6 -> 22
        public const int ITC00_SAD6 = 22;

        /// ITC00_SAD7 -> 23
        public const int ITC00_SAD7 = 23;

        /// ITC00_TEM0 -> 24
        public const int ITC00_TEM0 = 24;

        /// ITC00_TEM1 -> 25
        public const int ITC00_TEM1 = 25;

        /// ITC00_DIF0 -> 26
        public const int ITC00_DIF0 = 26;

        /// ITC00_DIS00 -> 27
        public const int ITC00_DIS00 = 27;

        /// ITC00_DIS01 -> 28
        public const int ITC00_DIS01 = 28;

        /// ITC00_DIF1 -> 29
        public const int ITC00_DIF1 = 29;

        /// ITC00_DIS10 -> 30
        public const int ITC00_DIS10 = 30;

        /// ITC00_DIS11 -> 31
        public const int ITC00_DIS11 = 31;

        /// ITC16_MINIMUM_SAMPLING_INTERVAL -> 5000
        public const int ITC16_MINIMUM_SAMPLING_INTERVAL = 5000;

        /// ITC16_MINIMUM_SAMPLING_STEP -> 1000
        public const int ITC16_MINIMUM_SAMPLING_STEP = 1000;

        /// ITC18_MINIMUM_SAMPLING_INTERVAL -> 5000
        public const int ITC18_MINIMUM_SAMPLING_INTERVAL = 5000;

        /// ITC18_MINIMUM_SAMPLING_STEP -> 1250
        public const int ITC18_MINIMUM_SAMPLING_STEP = 1250;

        /// ITC1600_MINIMUM_SAMPLING_INTERVAL -> 5000
        public const int ITC1600_MINIMUM_SAMPLING_INTERVAL = 5000;

        /// ITC1600_MINIMUM_SAMPLING_STEP -> 5000
        public const int ITC1600_MINIMUM_SAMPLING_STEP = 5000;

        /// ITC18_STANDARD_FUNCTION -> 0
        public const int ITC18_STANDARD_FUNCTION = 0;

        /// ITC18_PHASESHIFT_FUNCTION -> 1
        public const int ITC18_PHASESHIFT_FUNCTION = 1;

        /// ITC18_DYNAMICCLAMP_FUNCTION -> 2
        public const int ITC18_DYNAMICCLAMP_FUNCTION = 2;

        /// ITC18_SPECIAL_FUNCTION -> 3
        public const int ITC18_SPECIAL_FUNCTION = 3;

        /// ITC1600_STANDARD_FUNCTION -> 0
        public const int ITC1600_STANDARD_FUNCTION = 0;

        /// ITC1600_TEST_FUNCTION -> 0x10
        public const int ITC1600_TEST_FUNCTION = 16;

        /// ITC1600_S_OUTPUT_FUNCTION -> 0x11
        public const int ITC1600_S_OUTPUT_FUNCTION = 17;

        /// ITC1600_STANDARD_DSP -> 0
        public const int ITC1600_STANDARD_DSP = 0;

        /// ITC1600_TEST_DSP -> 4
        public const int ITC1600_TEST_DSP = 4;

        /// ITC1600_S_OUTPUT_DSP -> 6
        public const int ITC1600_S_OUTPUT_DSP = 6;

        /// ITC1600_STANDARD_HOST -> 0
        public const int ITC1600_STANDARD_HOST = 0;

        /// ITC1600_STANDARD_RACK -> 0
        public const int ITC1600_STANDARD_RACK = 0;

        /// ITC00_STANDARD_FUNCTION -> 0
        public const int ITC00_STANDARD_FUNCTION = 0;

        /// ITC00_TEST_FUNCTION -> 0x10
        public const int ITC00_TEST_FUNCTION = 16;

        /// ITC00_S_OUTPUT_FUNCTION -> 0x11
        public const int ITC00_S_OUTPUT_FUNCTION = 17;

        /// ITC00_STANDARD_DSP -> 0
        public const int ITC00_STANDARD_DSP = 0;

        /// ITC00_TEST_DSP -> 4
        public const int ITC00_TEST_DSP = 4;

        /// ITC00_S_OUTPUT_DSP -> 6
        public const int ITC00_S_OUTPUT_DSP = 6;

        /// ITC00_STANDARD_HOST -> 0
        public const int ITC00_STANDARD_HOST = 0;

        /// ITC00_STANDARD_RACK -> 0
        public const int ITC00_STANDARD_RACK = 0;

        /// ITC1600_INTERNAL_CLOCK -> 0x0
        public const int ITC1600_INTERNAL_CLOCK = 0;

        /// ITC1600_INTRABOX_CLOCK -> 0x1
        public const int ITC1600_INTRABOX_CLOCK = 1;

        /// ITC1600_EXTERNAL_CLOCK -> 0x2
        public const int ITC1600_EXTERNAL_CLOCK = 2;

        /// ITC1600_CLOCKMODE_MASK -> 0x3
        public const int ITC1600_CLOCKMODE_MASK = 3;

        /// ITC1600_PCI1600_RACK -> 0x8
        public const int ITC1600_PCI1600_RACK = 8;

        /// ITC1600_RACK_RELOAD -> 0x10
        public const int ITC1600_RACK_RELOAD = 16;

        /// ITC00_INTERNAL_CLOCK -> 0x0
        public const int ITC00_INTERNAL_CLOCK = 0;

        /// ITC00_EXTERNAL_CLOCK -> 0x1
        public const int ITC00_EXTERNAL_CLOCK = 1;

        /// ITC00_INTRABOX_CLOCK -> 0x2
        public const int ITC00_INTRABOX_CLOCK = 2;

        /// ITC00_CLOCKMODE_MASK -> 0x3
        public const int ITC00_CLOCKMODE_MASK = 3;

        /// ITC00_PCI1600_RACK -> 0x8
        public const int ITC00_PCI1600_RACK = 8;

        /// ITC00_RACK_RELOAD -> 0x10
        public const int ITC00_RACK_RELOAD = 16;

        /// DI_HEKA_ACTIVE_LOW -> 0x8000
        public const int DI_HEKA_ACTIVE_LOW = 32768;

        /// DI_HEKA_LATCHING_MODE -> 0x4000
        public const int DI_HEKA_LATCHING_MODE = 16384;

        /// DI_TRIGIN_ACTIVE_LOW -> 0x2000
        public const int DI_TRIGIN_ACTIVE_LOW = 8192;

        /// DI_TRIGIN_LATCHING_MODE -> 0x1000
        public const int DI_TRIGIN_LATCHING_MODE = 4096;

        /// DI_FRONT_3_2_ACTIVE_LOW -> 0x0800
        public const int DI_FRONT_3_2_ACTIVE_LOW = 2048;

        /// DI_FRONT_3_2_LATCHING_MODE -> 0x0400
        public const int DI_FRONT_3_2_LATCHING_MODE = 1024;

        /// DI_FRONT_1_0_ACTIVE_LOW -> 0x0200
        public const int DI_FRONT_1_0_ACTIVE_LOW = 512;

        /// DI_FRONT_1_0_LATCHING_MODE -> 0x0100
        public const int DI_FRONT_1_0_LATCHING_MODE = 256;

        /// DI_15_12_ACTIVE_LOW -> 0x0080
        public const int DI_15_12_ACTIVE_LOW = 128;

        /// DI_15_12_LATCHING_MODE -> 0x0040
        public const int DI_15_12_LATCHING_MODE = 64;

        /// DI_11_08_ACTIVE_LOW -> 0x0020
        public const int DI_11_08_ACTIVE_LOW = 32;

        /// DI_11_08_LATCHING_MODE -> 0x0010
        public const int DI_11_08_LATCHING_MODE = 16;

        /// DI_07_04_ACTIVE_LOW -> 0x0008
        public const int DI_07_04_ACTIVE_LOW = 8;

        /// DI_07_04_LATCHING_MODE -> 0x0004
        public const int DI_07_04_LATCHING_MODE = 4;

        /// DI_03_00_ACTIVE_LOW -> 0x0002
        public const int DI_03_00_ACTIVE_LOW = 2;

        /// DI_03_00_LATCHING_MODE -> 0x0001
        public const int DI_03_00_LATCHING_MODE = 1;

        /// ITC_READ_OVERFLOW_H -> 0x00010000
        public const int ITC_READ_OVERFLOW_H = 65536;

        /// ITC_WRITE_UNDERRUN_H -> 0x00020000
        public const int ITC_WRITE_UNDERRUN_H = 131072;

        /// ITC_READ_OVERFLOW_S -> 0x00100000
        public const int ITC_READ_OVERFLOW_S = 1048576;

        /// ITC_WRITE_UNDERRUN_S -> 0x00200000
        public const int ITC_WRITE_UNDERRUN_S = 2097152;

        /// ITC_STOP_CH_ON_OVERFLOW -> 0x00000001
        public const int ITC_STOP_CH_ON_OVERFLOW = 1;

        /// ITC_STOP_CH_ON_UNDERRUN -> 0x00000002
        public const int ITC_STOP_CH_ON_UNDERRUN = 2;

        /// ITC_STOP_CH_ON_COUNT -> 0x00000010
        public const int ITC_STOP_CH_ON_COUNT = 16;

        /// ITC_STOP_PR_ON_COUNT -> 0x00000020
        public const int ITC_STOP_PR_ON_COUNT = 32;

        /// ITC_STOP_DR_ON_OVERFLOW -> 0x00000100
        public const int ITC_STOP_DR_ON_OVERFLOW = 256;

        /// ITC_STOP_DR_ON_UNDERRUN -> 0x00000200
        public const int ITC_STOP_DR_ON_UNDERRUN = 512;

        /// ITC_STOP_ALL_ON_OVERFLOW -> 0x00001000
        public const int ITC_STOP_ALL_ON_OVERFLOW = 4096;

        /// ITC_STOP_ALL_ON_UNDERRUN -> 0x00002000
        public const int ITC_STOP_ALL_ON_UNDERRUN = 8192;

        /// PaulKey -> 0x5053
        public const int PaulKey = 20563;

        /// HekaKey -> 0x4845
        public const int HekaKey = 18501;

        /// UicKey -> 0x5543
        public const int UicKey = 21827;

        /// InstruKey -> 0x4954
        public const int InstruKey = 18772;

        /// AlexKey -> 0x4142
        public const int AlexKey = 16706;

        /// UWKey -> 0x5557
        public const int UWKey = 21847;

        /// EcellKey -> 0x4142
        public const int EcellKey = 16706;

        /// SampleKey -> 0x5470
        public const int SampleKey = 21616;

        /// TestKey -> 0x4444
        public const int TestKey = 17476;

        /// TestSuiteKey -> 0x5453
        public const int TestSuiteKey = 21587;

        /// DemoKey -> 0x4445
        public const int DemoKey = 17477;

        /// IgorKey -> 0x4947
        public const int IgorKey = 18759;

        /// CalibrationKey -> 0x4341
        public const int CalibrationKey = 17217;

        /// AcqUIKey -> 0x4151
        public const int AcqUIKey = 16721;

        /// ITC_EMPTY -> 0
        public const int ITC_EMPTY = 0;

        /// ITC_RESERVE -> 0x80000000
        public const int ITC_RESERVE = -2147483648;

        /// ITC_INIT_FLAG -> 0x00008000
        public const int ITC_INIT_FLAG = 32768;

        /// ITC_RACK_FLAG -> 0x00004000
        public const int ITC_RACK_FLAG = 16384;

        /// ITC_FUNCTION_MASK -> 0x00000FFF
        public const int ITC_FUNCTION_MASK = 4095;

        /// RUN_STATE -> 0x10
        public const int RUN_STATE = 16;

        /// ERROR_STATE -> 0x80000000
        public const int ERROR_STATE = -2147483648;

        /// DEAD_STATE -> 0x00
        public const int DEAD_STATE = 0;

        /// EMPTY_INPUT -> 0x01
        public const int EMPTY_INPUT = 1;

        /// EMPTY_OUTPUT -> 0x02
        public const int EMPTY_OUTPUT = 2;

        /// SAMPLING_MASK -> 0x03
        public const int SAMPLING_MASK = 3;

        /// USE_FREQUENCY -> 0x00
        public const int USE_FREQUENCY = 0;

        /// USE_TIME -> 0x01
        public const int USE_TIME = 1;

        /// USE_TICKS -> 0x02
        public const int USE_TICKS = 2;

        /// SCALE_MASK -> 0x0C
        public const int SCALE_MASK = 12;

        /// NO_SCALE -> 0x00
        public const int NO_SCALE = 0;

        /// MS_SCALE -> 0x04
        public const int MS_SCALE = 4;

        /// US_SCALE -> 0x08
        public const int US_SCALE = 8;

        /// NS_SCALE -> 0x0C
        public const int NS_SCALE = 12;

        /// ADJUST_RATE -> 0x10
        public const int ADJUST_RATE = 16;

        /// DONTIGNORE_SCAN -> 0x20
        public const int DONTIGNORE_SCAN = 32;

        /// ANALOGVOLT -> 3200.
        public const float ANALOGVOLT = 3200F;

        /// SLOWANALOGVOLT -> 3276.7
        public const float SLOWANALOGVOLT = 3276.7F;

        /// OFFSETINVOLTS -> 819200.
        public const float OFFSETINVOLTS = 819200F;

        /// SLOWOFFSETINVOLTS -> 838835.2
        public const float SLOWOFFSETINVOLTS = 838835.2F;

        /// POSITIVEVOLT -> 10.2396875
        public const float POSITIVEVOLT = 10.23969F;

        /// SLOWPOSITIVEVOLT -> 10.
        public const float SLOWPOSITIVEVOLT = 10F;

        /// ITC18_GAIN_1 -> 1
        public const int ITC18_GAIN_1 = 1;

        /// ITC18_GAIN_2 -> 2
        public const int ITC18_GAIN_2 = 2;

        /// ITC18_GAIN_5 -> 5
        public const int ITC18_GAIN_5 = 5;

        /// ITC18_GAIN_10 -> 10
        public const int ITC18_GAIN_10 = 10;
    }
}
