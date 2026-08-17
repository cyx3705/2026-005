s=tf('s');

G4=11.55/(s*(s^2+6.14*s+36.1));

t=0:0.01:20;
u=ones(size(t));
L=lsim(G1,u,t);
plot(t,L);
hold on;

n=1;
plot(t,u);
