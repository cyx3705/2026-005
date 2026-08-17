s=tf('s');
m=1;
c=3;
k=0:3:5;
G1=10/((s+5)*(s+2));
sys=G1/(1+G1)

t=0:0.01:20;
u=ones(size(t));
L=lsim(sys,u,t);
plot(t,L);


