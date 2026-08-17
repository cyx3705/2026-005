s=tf('s');

G1=10/(s^2+7*s+20);

t=0:0.01:20;
u=ones(size(t));
L=lsim(G1,u,t);
plot(t,L);
hold on;

n=1;
plot(t,u);
