s=tf('s');

c1=2*(s+6)/((s+2)*(s+4));

t=0:0.01:20;
u=ones(size(t));
L=lsim(c1,u,t);
plot(t,L);
hold on;


