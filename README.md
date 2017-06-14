# SeniorDesignCode
CLI code for senior design project.

Compile from command line: 
csc /lib:c:\ /reference:Interop.MLApp.dll /reference:Ivi.Visa.Interop.dll Calibrate.cs

Usage: 
Calirate [AWG_Frequency]

Other Notes: 

-Must include MATLAB function myfunc.m in a folder named "temp" in C drive for Calibrate.cs to send samples to matlab properly. 

-This C# CLI recieves unordered samples from the FIFO USB driver and reorders them based on Random Equivalent Time Sampling method aka the "beat frequency" method.

