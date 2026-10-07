namespace SecuritySystem.Modules.Brinks;


public enum LcdLedArmedState : byte
{
    Off = 0,
    OnSolid = 1,
    OnFlashing1 = 2,
    OnFlashing2 = 3,
    OnFlashing3 = 3,
    OnFlashing4 = 4,
    OnFlashing5 = 5,
    OnFlashing6 = 6,
    OnFlashing7 = 7
}
public delegate void OnKey(LcdKey key);
public enum LcdBit
{
    Colon = 0,
    Digit0B = 1,
    Digit0C = 2,
    Am = 3,
    Pm = 4,

    Digit1A = 8,
    Digit1B = 9,
    Digit1C = 10,
    Digit1D = 11,
    Digit1E = 12,
    Digit1F = 13,
    Digit1G = 14,

    Digit2A = 16,
    Digit2B = 17,
    Digit2C = 18,
    Digit2D = 19,
    Digit2E = 20,
    Digit2F = 21,
    Digit2G = 22,

    Digit3A = 24,
    Digit3B = 25,
    Digit3C = 26,
    Digit3D = 27,
    Digit3E = 28,
    Digit3F = 29,
    Digit3G = 30,

    INot = 32,
    IReady = 33,
    IAll = 34,
    IOn = 35,
    IInstant = 36,
    IDoorChime = 37,
    INoAc = 38,
    IMotionOff = 39,

    ITrouble = 40,
    ITest = 41,
    ILineCut = 42,
    ILowBatt = 43,
    IAlarm = 44,
    IMemory = 45,
    IAuxiliaryCodes = 46,
    ICanceled = 47,

    IBypass = 48,
    IEnter = 49,
    IMaster = 50,
    IUser = 51,
    ICallBrinks = 52,
    IZone = 53,
    INew = 54,
    ICode = 55,

    IArea = 56,
    INumber = 57,
    IEnvelope = 58,
    ITime = 59
}

/// <summary>
/// Byte level representation of keypad LCD state
/// </summary>
public class KeypadLcd
{
    public bool[] LCD = new bool[64];
    private static readonly byte[] DigitPatterns =
    {
        0b00111111, // 0
        0b00000110, // 1
        0b01011011, // 2
        0b01001111, // 3
        0b01100110, // 4
        0b01101101, // 5
        0b01111101, // 6
        0b00000111, // 7
        0b01111111, // 8
        0b01101111  // 9
    };
    // Only characters with reasonably recognizable 7-segment representations.
    private static readonly Dictionary<char, byte> CharacterPatterns =
        new()
        {
            ['A'] = 0b01110111,
            ['B'] = 0b01111100, // Looks like lowercase b
            ['C'] = 0b00111001,
            ['D'] = 0b01011110, // Looks like lowercase d
            ['E'] = 0b01111001,
            ['F'] = 0b01110001,
            ['H'] = 0b01110110,
            ['J'] = 0b00011110,
            ['L'] = 0b00111000,
            ['P'] = 0b01110011,
            ['U'] = 0b00111110,
            ['R'] = 0b01010000, // lowercase r

            ['-'] = 0b01000000,
            ['_'] = 0b00001000,
            [' '] = 0b00000000
        };
    public void Clear()
    {
        LCD = new bool[64];
    }

    public void SetHour1st(bool on)
    {
        Set(LcdBit.Digit0B, on);
        Set(LcdBit.Digit0C, on);
    }

    public void SetDigit(int digitPosition, int digit)
    {
        if (digitPosition < 0 || digitPosition > 2)
            throw new ArgumentOutOfRangeException(nameof(digitPosition));

        if (digit < 0 || digit > 9)
            throw new ArgumentOutOfRangeException(nameof(digit));

        LcdBit firstSegment = digitPosition switch
        {
            0 => LcdBit.Digit1A,
            1 => LcdBit.Digit2A,
            2 => LcdBit.Digit3A,
            _ => throw new ArgumentOutOfRangeException(nameof(digitPosition))
        };

        byte pattern = DigitPatterns[digit];

        for (int i = 0; i < 7; i++)
        {
            Set(
                (LcdBit)((int)firstSegment + i),
                (pattern & (1 << i)) != 0
            );
        }
    }


    private void SetCharacterPattern(int characterPosition, byte pattern)
    {
        LcdBit firstSegment = characterPosition switch
        {
            0 => LcdBit.Digit1A,
            1 => LcdBit.Digit2A,
            2 => LcdBit.Digit3A,
            _ => throw new ArgumentOutOfRangeException(nameof(characterPosition))
        };

        for (int i = 0; i < 7; i++)
        {
            Set(
                (LcdBit)((int)firstSegment + i),
                (pattern & (1 << i)) != 0
            );
        }
    }

    public void SetCharacter(int characterPosition, char character)
    {
        if (characterPosition < 0 || characterPosition > 2)
            throw new ArgumentOutOfRangeException(nameof(characterPosition));

        character = char.ToUpperInvariant(character);

        if (!CharacterPatterns.TryGetValue(character, out byte pattern))
        {
            throw new ArgumentException(
                $"Character '{character}' cannot be displayed on a 7-segment display.",
                nameof(character));
        }

        SetCharacterPattern(characterPosition, pattern);
    }

    public void Set(LcdBit bit, bool state)
    {
        LCD[(int)bit] = state;
    }

    public byte[] Build()
    {
        byte[] lcd = new byte[9];
        for (int i = 0; i < 8; i++)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                if (LCD[i * 8 + bit])
                    lcd[i + 1] |= (byte)(1 << bit);
            }
        }
        return lcd;
    }
}
