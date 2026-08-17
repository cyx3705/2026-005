S=tf('s');
r=10;
c=0.001;
fh1=r*c*S/(r*c*S+1);
'bode(fh1);'
t=0:0.00001:0.1;

u=sin(1429.88*t);
'plot(t,u);'
uo=lsim(fh1,u,t);
hold on;
'plot(t,uo);'

t=0:0.00001:2;
u=sin(3.74*t);
plot(t,u);
uo=lsim(fh1,u,t);
hold on;
'plot(t,uo);'

t=0:0.0001:2;
u1=sin(32*t);
u2=0.2*sin(486*t);
ui=u1+u2;
plot(t,ui);
uo=lsim(fh1,ui,t);
hold on
plot(t,uo);