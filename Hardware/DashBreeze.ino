// Author: Jesus Altuve
// Date 6/25/2023 @ 9:47 GMT -4
// DashBreeze v1.0

const uint8_t SLEEP_DUTY_CYCLE  = 0;
const uint8_t MIN_DUTY_CYCLE    = 10;
const uint8_t MAX_DUTY_CYCLE    = 100;
const byte CHANGE_STATE_SIGNAL  = 255;
const byte CURRENT_STATE_SIGNAL = 254;
const char HandShakeRcv[]       = "BINGO";
const char uniqueIDresponse[]   = "ACK";
bool isSleeping                 = false;

uint8_t pwmValue1 = MIN_DUTY_CYCLE; // Start at 10% duty cycle
uint8_t pwmValue2 = MIN_DUTY_CYCLE; // Start at 10% duty cycle
uint8_t pwmOCR[101]; // Pre-calculate (ICR1 * pwmValue) / 100
unsigned long lastSerialEvent = 0; // Keep track of the time of the last serial event
int timeout = 100; // Sleep mode for fans when no commands are received in milliseconds

void setup() {
  DDRB |= (1<<PB5) | (1<<PB6); // Set 9 and 10 as PWM Pins (Works with Leonardo only)
  // DDRB |= (1<<PB1) | (1<<PB2); // Set PINs 9 and 10 on the Arduino UNO R3
  
  // Start the serial port at 115200 baud
  Serial.begin(115200);  
  while (!Serial)
    delayMicroseconds(1);

  // Set PWM frequency to 25kHz
  TCCR1A = (1 << WGM11) | (1 << COM1A1) | (1 << COM1B1);
  TCCR1B = (1 << WGM13) | (1 << WGM12) | (1 << CS10);
  ICR1 = 160; //TOP value - 16000000 / (1*25*10^3) - 1

  // Pre-compute pwmOCR for all possible pwmValues
  for(int i = 0; i <= 100; i++)
  {
    pwmOCR[i] = (ICR1 * i) / 100;
  }
}

void loop() {
  // If no serial communication for {timeout} seconds, set PWM values to 0%
  if (!isSleeping && timeout > 0 && ((millis() - lastSerialEvent) > timeout)) {
    OCR1A = pwmOCR[SLEEP_DUTY_CYCLE];
    OCR1B = pwmOCR[SLEEP_DUTY_CYCLE];
    isSleeping = true; // Set the sleep mode flag
  }
  while (Serial.available() >= 2) {
    // Clear the sleep mode flag
    isSleeping = false;
    
    byte incomingByte1 = Serial.read();
    byte incomingByte2 = Serial.read();

    // A basic way to implement device autodetect
    if(incomingByte1 == CHANGE_STATE_SIGNAL && incomingByte2 == CURRENT_STATE_SIGNAL) {
      Serial.println(HandShakeRcv);
      pwmValue1 = MIN_DUTY_CYCLE;
      pwmValue2 = MIN_DUTY_CYCLE;
    }
    else if(incomingByte1 == CHANGE_STATE_SIGNAL && incomingByte2 == CHANGE_STATE_SIGNAL) {
      Serial.println(uniqueIDresponse);
      pwmValue1 = MIN_DUTY_CYCLE;
      pwmValue2 = MIN_DUTY_CYCLE;
    }
    else if( (incomingByte1 >= 0 && incomingByte1 <= 100) && (incomingByte2 >= 0 && incomingByte2 <= 100) ) {
      // Use min and max to constrain incomingByte values between 0 and 100
      incomingByte1 = max(MIN_DUTY_CYCLE, min(MAX_DUTY_CYCLE, incomingByte1));
      incomingByte2 = max(MIN_DUTY_CYCLE, min(MAX_DUTY_CYCLE, incomingByte2));
      pwmValue1 = (uint8_t)incomingByte1;
      pwmValue2 = (uint8_t)incomingByte2;
    }
    else {
      pwmValue1 = MIN_DUTY_CYCLE;
      pwmValue2 = MIN_DUTY_CYCLE;      
    }

    OCR1A = pwmOCR[pwmValue1];
    OCR1B = pwmOCR[pwmValue2];
    
    // Update the time of the last serial event
    lastSerialEvent = millis();
  }
}
