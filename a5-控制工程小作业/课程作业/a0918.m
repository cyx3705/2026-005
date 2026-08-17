s=tf('s');
m=1;
c=3;
k=0:3:5;
G1=1/(m*s^2+c*s+k);

t=0:0.01:20;
u=ones(size(t));
L=lsim(G1,u,t);
plot(t,L);
hold on;
n=1;
plot(t,u);
