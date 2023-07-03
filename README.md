# DashBreeze

DashBreeze is a SimHub plugin designed to control fans based on your simulator's speed telemetry data, simulating the feeling of wind rushing against your face.

## Features

- Maps in-game speed to a fan speed percentage.
- Adjusts fan speed based on a customizable intensity setting.
- Automatically detects and connects to compatible devices via serial ports.

## Installation

To install the DashBreeze plugin, download the DLL file from the repository and place it in the same directory where SimHub is installed. If you don't have the DashBreeze control unit you can use an Arduino Leonardo and upload the custom sketch included in the Hardware directory.

## Usage

Once installed, DashBreeze should be available as a plugin in SimHub. Refer to SimHub's plugin usage guide for further instructions.

## Contributing

While we appreciate your interest in DashBreeze, we are currently not accepting any new features. However, if you encounter a bug and would like to propose a fix, here's how you can do that:

1. Fork this repository.
2. Create a branch (`git checkout -b my-bugfix`).
3. Commit your changes (`git commit -m 'Fixed a bug'`).
4. Push to the branch (`git push origin my-bugfix`).
5. Open a Pull Request.

Before proposing a bugfix, please check the existing issues to see if someone else has already reported the problem. If it's already reported, you can contribute by working on the fix.

## License
MIT License

Copyright (c) 2023 Jesus Altuve

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
