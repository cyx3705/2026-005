S=tf('s');
r=10;
c=0.001;
fh1=1/(r*c*S+1);
fh2=fh1*fh1*fh1;
bode(fh1);
bode(fh2);
t=0:0.00001:0.1;

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

t=0:0.0001:2;
u1=sin(32*t);
u2=0.2*sin(486*t);
ui=u1+u2;
plot(t,ui);
uo=lsim(fh2,ui,t);
hold on
plot(t,uo);