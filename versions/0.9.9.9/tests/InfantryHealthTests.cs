using System;
using System.IO;
using GhpcCoop;
class InfantryHealthTests {
 static void Main() {
  if(InfantryHealth.Parse("",0).Length!=0)throw new Exception();
  var values=InfantryHealth.Parse("1,0.5,0",3);
  if(values[0]!=1||values[1]!=.5f||values[2]!=0)throw new Exception();
  foreach(var text in new[]{"NaN","Infinity","-1","1.1","garbage","0,1"}){
   bool rejected=false;try{InfantryHealth.Parse(text,1);}catch(InvalidDataException){rejected=true;}
   if(!rejected)throw new Exception("Accepted invalid damage "+text);
  }
  Console.WriteLine("PASS 8 infantry health validation cases");
 }
}
