package android.view;
public class View {
  public static final int VISIBLE=0, GONE=8;
  public interface OnApplyWindowInsetsListener { WindowInsets onApplyWindowInsets(View v, WindowInsets i); }
  public interface OnLayoutChangeListener { void onLayoutChange(View v, int l, int t, int r, int b, int ol, int ot, int or, int ob); }
  public void setOnApplyWindowInsetsListener(OnApplyWindowInsetsListener l){}
  public void addOnLayoutChangeListener(OnLayoutChangeListener l){}
  public void setPadding(int l,int t,int r,int b){}
  public void requestApplyInsets(){}
  public void setVisibility(int v){}
  public int getVisibility(){return 0;}
  public int getHeight(){return 0;}
  public ViewParent getParent(){return null;}
  public boolean post(Runnable r){return true;}
}
