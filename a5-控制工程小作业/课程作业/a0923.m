s=tf('s');

G1=1.33/(7.45*s+1);

t=0:0.01:20;
u=ones(size(t));
L=lsim(G1,u,t);
plot(t,L);
hold on;

n=1;
plot(t,u);
