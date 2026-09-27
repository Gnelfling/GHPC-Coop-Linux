using System;
using GhpcCoop;
class InfantryMotionTests
{
 static int checks;
 static void Near(float actual,float expected){if(Math.Abs(actual-expected)>.0001f)throw new Exception(actual+" != "+expected);checks++;}
 static void Main(){
  var m=new InfantryMotion();m.Receive(0,0,0,1,false);Near(m.X,0);
  m.Receive(1,0,0,1.1f,false);Near(m.X,0);m.Render(1.15f);Near(m.X,.5f);m.Render(1.2f);Near(m.X,1);
  m.Receive(2,0,0,1.2f,false);m.Render(1.25f);Near(m.X,1.5f);
  m.Receive(3,0,0,1.25f,false);Near(m.X,1.5f);m.Render(1.275f);Near(m.X,2.25f);m.Render(2);Near(m.X,3);
  m.Receive(20,4,8,2.1f,false);Near(m.X,20);Near(m.Y,4);Near(m.Z,8);
  m.Receive(21,4,8,2.2f,true);Near(m.X,21);
  m.Receive(22,4,8,3,false);Near(m.X,22);
  m.Receive(23,4,8,3.1f,false);m.Render(3);Near(m.X,22);m.Render(9);Near(m.X,23);
  Console.WriteLine("PASS "+checks+" infantry motion interpolation checks");
 }
}
